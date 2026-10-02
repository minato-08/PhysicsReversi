# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

物理演算で石を扱うリバーシの Unity 試作（Unity 6, URP, Input System。コミット済みは `6000.4.3f1`、環境によってはより新しい版で開いている）。プレイヤーが歩き回り、石を掴んで盤に置き、**その時点で上を向いている面の色**で所属が決まる（静止は待たない）。現状の仕様・操作・調整項目は `README.md`（日本語）、目指す仕様と未確定事項は `SPEC.md` にまとまっているので、ルール変更時は両方を更新する。

複数の環境（PC）から作業するため、`CLAUDE.md` などのコンテキストファイルはコミット・Push してよい。`ProjectSettings/ProjectVersion.txt` は環境ごとの Unity パッチ版差で書き換わるので、意図しない限りコミットしない。

## ビルド・テスト

CLI ビルドスクリプトや Unity Test Framework のテストはない。検証は Unity エディタ上で行う。

- **ルールテスト**: メニュー `Physics Reversi → Run Rule Checks`（`Assets/PhysicsReversi/Editor/PrototypeSetup.cs`）。`RulesChecks.Run()` が純粋ロジックを検査し、失敗時は `"FAILED: <名前>"` の例外を投げる。成功時は通過件数をログ出力。
  - バッチ実行する場合（`Unity.exe` は Windows の例。macOS は `/Applications/Unity/Hub/Editor/<版>/Unity.app/Contents/MacOS/Unity`）: `Unity.exe -batchmode -projectPath . -executeMethod PhysicsReversi.Editor.PrototypeSetup.RunRuleChecks -quit -logFile -`
  - 個別テストの実行機構はない。`RulesChecks.cs`（Editor フォルダにあるが namespace は `PhysicsReversi`）は1本のメソッドに `Check(...); checks++;` を並べた形式なので、テスト追加もこの形式に合わせる。
- コンパイルエラー確認・Play・シーン操作は Unity CLI（`unity` コマンド、Unity プラグインの `unity:unity-cli` スキル）で開いているエディタを操作して行う。`unity status` で接続を確認する。プロジェクト側に `com.unity.pipeline` パッケージが必要。Coplay MCP は使わない。
  - Play を CLI から検証するとき、エディタが最前面でないとゲームが進まない。Play 開始後に `unity command eval 'UnityEngine.Application.runInBackground = true; return 1;'` を実行する。この値はプロジェクト設定（`ProjectSettings.asset` の `runInBackground`）に残るので、検証が終わったら編集モードで `UnityEditor.PlayerSettings.runInBackground = false; UnityEditor.AssetDatabase.SaveAssets();` を実行して戻す。画面撮影は `capture_game_view --source screen`（保存先は `Assets/` 配下に限られるので、撮影後に `.meta` ごと片付ける）。
  - Play 中でないときに `eval` でシーン上のオブジェクトの値を書き換えると、編集中のシーンが変わってしまう。検証用の `eval` は先頭で `EditorApplication.isPlaying` を確認する。
- 見た目・反転の手触り・衝突結果は Play での手動確認が必要。

## アーキテクチャ

asmdef はなく、すべて `Assembly-CSharp` / `Assembly-CSharp-Editor` に入る。

### 3層構造

1. **純粋ルール層** `Assets/PhysicsReversi/Scripts/`（namespace `PhysicsReversi`）— Unity 非依存にしてあり、`RulesChecks` から直接テストされる。
   - `BoardRules`: 盤座標は -4〜+4（1マス=1単位）、64マス（`cell = z*8 + x`）。石の投影凸包ポリゴンとマスの重なり面積比で認識（`Recognize`）し、`Snapshot`（Ids/Owners 配列）を作る。`Captures` は挟み判定のみで盤を変更しない。
   - `StoneFaces.Owner(upDot)`: 上面方向との内積から所属を決める（黒=1 が +Y、白=2 が -Y、横倒しは 0）。
   - `RealtimeCaptures`: 現行 Walk モードの捕獲判定。配置イベントではなく「ラインの形成」で発火し、ライン単位のラッチで同じ並びの再発火や色反転での振動を防ぐ。
   - `MotionOrigin`: 石の動きの原因（プレイヤー起因か、反転演出起因か）。**プレイヤー起因の動きだけが捕獲を起こせる**。捕獲すると同一アクションで動いた石すべての原因が消費され、反転が新たな捕獲を連鎖させない。
   - `PlacementCaptures`: 配置キュー方式の旧捕獲判定（全配置を同一スナップショットで評価）。現在は `RulesChecks` からしか呼ばれない。
2. **Walk 実行層** `Assets/PhysicsReversi/Walk/`（namespace `PhysicsReversi.Walk`）— シーン `Assets/Scenes/PhysicsReversiWalk.unity`。
   - `WalkBoardRecognition`: `FixedUpdate` で一定間隔ごとに石のメッシュ頂点を盤平面へ投影し `BoardRules.Recognize` → `SnapshotConfirmed` イベント。静止判定はなく、盤に接地している `OnBoard` の石は動いていても毎回 `CarryStone.ReadUpperFace` で `ownerId` を書き換える。盤の `cellWidth`（ワールド単位）を正規化座標へ変換している。
   - `WalkCaptureController`: `SnapshotConfirmed` を購読し `RealtimeCaptures.Scan` を実行、対象石を Rigidbody のまま物理的に180度回す（色の塗り替えや所属の強制変更はしない）。反転中の石は一時的に判定対象外。
   - `CarryAuthority`: 掴む・離す（保持と `status` の変更）はすべてここを経由する（将来のネットワーク権威の置き場所）。入力コードから直接書き換えないこと。盤上の所属 `ownerId` はここではなく認識処理が書く。床から落ちた石を予備へ戻す処理（`FixedUpdate` → `ReturnToReserve`）もここにある。掴める石の範囲は未確定で、`allowPlacedStonePickup` / `allowOpponentStonePickup` を Inspector で切り替えて試している（現シーンは両方オン）。
   - `LocalWalkInput`: 1 台・1 画面で 2 人を同時に操作する入力。`players[0]` が黒（1 台目のゲームパッド、WASD+F）、`players[1]` が白（2 台目、IJKL+H）。操作は移動方向と掴む・離すの 1 ボタンだけで、移動は固定カメラ `view` 基準。照準はなく、正面のいちばん近い掴める石（`Target(index)`）を `CarryAuthority` に渡す。`AimHud` はプレイヤーごとにその石の輪郭を強調する表示専用（`WalkAssets/SilhouetteHighlight.shader` をマスク・線・塗りの3マテリアルで使い、輪郭の内側に線を描く）。カメラはスクリプトなしの固定（盤の横から見下ろし、黒が左・白が右）。持ち石は `WalkPlayer.carryPoint`（正面）に浮く。
   - `CarryStone`: 石ごとの状態。`Owner Id`（上面から決まる盤上の所属）と `Reserve Owner Id`（予備石の持ち主）を別管理。
3. **エディタセットアップ層** `Assets/PhysicsReversi/Editor/`（namespace `PhysicsReversi.Editor`）— シーン構築は手作業ではなく `Physics Reversi/Walk/...` メニューのスクリプトで行う（Scene Parts 配置、Recognition Rings、Capture Rules、Two-Sided Stones、Capture Practice、Play HUD、Second Player、Score HUD）。いずれも Play 停止中・`PhysicsReversiWalk` シーンで実行し、既存オブジェクトがあれば重複追加しない冪等な作り。生成アセットは `Assets/PhysicsReversi/WalkAssets/`。

### 旧プロトタイプ

`Assets/PhysicsReversi/Prototype.unity` + `PrototypeGame.cs` + `TuningConfig`（`Tuning.asset`）はランチャーで石を撃つ初期版。`BoardRules` を共有しているため、ルール層を変更するときは両方への影響を考慮する。

## 未実装（README より）

手番、対局終了、微振動の強制収束、通信同期（オンラインは当面の目標外）。黒(1)・白(2)の2人が1台のPCで同時に操作する。
