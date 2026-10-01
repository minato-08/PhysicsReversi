# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

物理演算で石を扱うリバーシの Unity 試作（Unity 6 / `6000.4.x`, URP, Input System）。プレイヤーが歩き回り、予備石を掴んで盤に置き、**静止後の石の上面の色**で所属が決まる。ユーザー向けの仕様・操作・調整項目は `README.md`（日本語）にまとまっているので、ルール変更時はそちらも更新する。

複数の環境（PC）から作業するため、`CLAUDE.md` などのコンテキストファイルはコミット・Push してよい。`ProjectSettings/ProjectVersion.txt` は環境ごとの Unity パッチ版差で書き換わるので、意図しない限りコミットしない。

## ビルド・テスト

CLI ビルドスクリプトや Unity Test Framework のテストはない。検証は Unity エディタ上で行う。

- **ルールテスト**: メニュー `Physics Reversi → Run Rule Checks`（`Assets/PhysicsReversi/Editor/PrototypeSetup.cs`）。`RulesChecks.Run()` が純粋ロジックを検査し、失敗時は `"FAILED: <名前>"` の例外を投げる。成功時は通過件数をログ出力。
  - バッチ実行する場合: `Unity.exe -batchmode -projectPath . -executeMethod PhysicsReversi.Editor.PrototypeSetup.RunRuleChecks -quit -logFile -`
  - 個別テストの実行機構はない。`RulesChecks.cs` は1本のメソッドに `Check(...); checks++;` を並べた形式なので、テスト追加もこの形式に合わせる。
- コンパイルエラー確認・シーン操作は Coplay MCP（`mcp__coplay-mcp__check_compile_errors`, `get_unity_logs` 等）が使える環境ならそれを使う。
- 見た目・反転の手触り・衝突結果は Play での手動確認が必要。

## アーキテクチャ

asmdef はなく、すべて `Assembly-CSharp` / `Assembly-CSharp-Editor` に入る。

### 3層構造

1. **純粋ルール層** `Assets/PhysicsReversi/Scripts/`（namespace `PhysicsReversi`）— Unity 非依存にしてあり、`RulesChecks` から直接テストされる。
   - `BoardRules`: 盤座標は -4〜+4（1マス=1単位）、64マス（`cell = z*8 + x`）。石の投影凸包ポリゴンとマスの重なり面積比で認識（`Recognize`）し、`Snapshot`（Ids/Owners 配列）を作る。`Captures` は挟み判定のみで盤を変更しない。
   - `StoneFaces.Owner(upDot)`: 上面方向との内積から所属を決める（黒=1 が +Y、白=2 が -Y、横倒しは 0）。
   - `RealtimeCaptures`: 現行 Walk モードの捕獲判定。配置イベントではなく「ラインの形成」で発火し、ライン単位のラッチで同じ並びの再発火や色反転での振動を防ぐ。
   - `MotionOrigin`: 石の動きの原因（プレイヤー起因か、反転演出起因か）。**プレイヤー起因の動きだけが捕獲を起こせる**。捕獲すると同一アクションで動いた石すべての原因が消費され、反転が新たな捕獲を連鎖させない。
   - `PlacementCaptures`: 配置キュー方式の旧捕獲判定（全配置を同一スナップショットで評価）。
2. **Walk 実行層** `Assets/PhysicsReversi/Walk/`（namespace `PhysicsReversi.Walk`）— シーン `Assets/Scenes/PhysicsReversiWalk.unity`。
   - `WalkBoardRecognition`: `FixedUpdate` で一定間隔ごとに石のメッシュ頂点を盤平面へ投影し `BoardRules.Recognize` → `SnapshotConfirmed` イベント。盤の `cellWidth`（ワールド単位）を正規化座標へ変換している。
   - `WalkCaptureController`: `SnapshotConfirmed` を購読し `RealtimeCaptures.Scan` を実行、対象石を Rigidbody のまま物理的に180度回す（色の塗り替えや所属の強制変更はしない）。反転中の石は一時的に判定対象外。
   - `CarryAuthority`: 所属・保持の変更はすべてここを経由する（将来のネットワーク権威の置き場所）。入力コードから所属を直接書き換えないこと。
   - `CarryStone`: 石ごとの状態。`Owner Id`（上面から決まる盤上の所属）と `Reserve Owner Id`（予備石の持ち主）を別管理。
3. **エディタセットアップ層** `Assets/PhysicsReversi/Editor/`（namespace `PhysicsReversi.Editor`）— シーン構築は手作業ではなく `Physics Reversi/Walk/...` メニューのスクリプトで行う（Scene Parts 配置、Recognition Rings、Capture Rules、Two-Sided Stones、Score HUD、Add Second Player）。いずれも Play 停止中・`PhysicsReversiWalk` シーンで実行し、既存オブジェクトがあれば重複追加しない冪等な作り。生成アセットは `Assets/PhysicsReversi/WalkAssets/`。

### 旧プロトタイプ

`Assets/PhysicsReversi/Prototype.unity` + `PrototypeGame.cs` + `TuningConfig`（`Tuning.asset`）はランチャーで石を撃つ初期版。`BoardRules` を共有しているため、ルール層を変更するときは両方への影響を考慮する。

## 未実装（README より）

対局終了、盤外回収、微振動の強制収束、通信同期。手番は設けずリアルタイム対戦とする方針。プレイヤーは黒(1)・白(2)の2人で、現状は `LocalWalkInput` の Tab で1台のPCから切り替えて操作する。
