# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

物理演算で石を扱うリバーシのUnity試作。Unity 6、URP、Input Systemを使う。コミット済みのUnityは`6000.4.3f1`で、環境によってはより新しい版で開いている。

プレイヤーが歩き回り、石を掴んで盤に置く。石の所属は**その時点で上を向いている面の色**で決まり、静止は待たない。

現状の仕様、操作、調整項目は`README.md`（日本語）に、目指す仕様と未確定事項は`SPEC.md`にまとめてある。ルールを変えるときは両方を更新する。

複数のPCから作業するため、`CLAUDE.md`などのコンテキストファイルはコミットしてPushしてよい。`ProjectSettings/ProjectVersion.txt`は、PCごとのUnityパッチ版の違いで書き換わるので、意図しない限りコミットしない。

## 進め方（シーン・コミット・ブランチ）

- **mainは採用した版を置く場所**。mainは直接編集せず、作業はブランチで行う。ブランチは「1つの方針の試み」で、採用すると決めたらmainへマージし、採用しないならマージしない。複数のブランチの内容は混在させない。
- 採用した版は早めにmainへ入れ、次の作業は新しいmainから分岐する。別の方針を比べたいときは、同じ分岐点からブランチを2本作り、1本だけを採用する。不採用のブランチから一部が欲しくなったら、マージせず、採用したブランチで書き直す。
- **シーンは同時に1本のブランチでしか編集しない**。シーンファイルのコンフリクトは手で解決できない。コンフリクトしたら、一方のブランチのシーンを丸ごと採用し、他方のブランチの変更はセットアップメニューを再実行して作り直す。手作業の配置調整は再現できないので注意する。
- **新しい試みや大幅な変更は、既存シーンを直接編集せず、複製シーンか新規シーンで行う**。採用が決まったら本体のシーンへ反映し、試作シーンは消す。セットアップメニューは、シーン名が`PhysicsReversiWalk`と一致するかを確認している。複製先で使うときは、この条件を直す。
- **方針が決まった後の古い版は、シーンやスクリプトとして残さず、gitの履歴にだけ残す**。動く状態で残すと、ルール変更のたびに新旧の両方を直すことになる。動かないまま残すと、後で意図が分からなくなる。残すのは、近いうちに開いて操作を比べる予定があるときだけ。お椀型のシーン`PhysicsReversiWalk`は例外で、盤を平面に決めた後も、ユーザーの判断で消さずに残している（2026-10-06）。動く状態を保つ対象ではないので、ルールを変えてもお椀型のシーンは直さない。
- 古い版をgitの履歴にだけ残す前提として、**区切りごとに細かくコミットする**。区切りは、動作確認が取れたとき、大きな作り替えに入る前、ドキュメントを更新したとき。
- **計画（`SPEC.md`）、実装（`README.md`とコード）、いま考えていることが食い違ってきたら、実装を進める前に文書を直す**。食い違いは`SPEC.md`の「計画と実装の食い違い」に書き出し、検討中の考えは「決まっていないこと」に候補として書く。決まっていない候補は実装しない。
- **ルールを実装する前に、そのルールが前提にしている行為（押す、当てる、など）がシーンの上で実際に起こせるかをPlayで測る**。コードに処理があっても、シーンでその行為を起こせるとは限らない（下の「シーンの物理」を参照）。

## ビルド・テスト

CLIビルドスクリプトやUnity Test Frameworkのテストはない。検証はUnityエディタ上で行う。

- **ルールテスト**は、メニュー`Physics Reversi → Run Rule Checks`で実行する。メニューの実装は`Assets/PhysicsReversi/Editor/PrototypeSetup.cs`にある。`RulesChecks.Run()`が純粋ロジックを検査し、失敗時は`"FAILED: <名前>"`の例外を投げる。成功時は通過件数をログに出力する。
  - バッチ実行するコマンドは`Unity.exe -batchmode -projectPath . -executeMethod PhysicsReversi.Editor.PrototypeSetup.RunRuleChecks -quit -logFile -`。`Unity.exe`はWindowsの例で、macOSでは`/Applications/Unity/Hub/Editor/<版>/Unity.app/Contents/MacOS/Unity`を使う。
  - 純粋ルール層と`RulesChecks.cs`はUnityに依存しない。`dotnet`があれば、`Scripts/`の純粋ロジックのファイルと`Editor/RulesChecks.cs`だけを並べた一時プロジェクトを作り、`RulesChecks.Run()`を呼ぶと、Unityを起動せずに検査できる。一時プロジェクトはリポジトリの外に作る。
  - 個別のテストを実行する仕組みはない。`RulesChecks.cs`は、1本のメソッドに`Check(...); checks++;`を並べた形式なので、テストを追加するときもこの形式に合わせる。このファイルはEditorフォルダにあるが、namespaceは`PhysicsReversi`。
- コンパイルエラーの確認、Play、シーン操作は、Unity CLI（`unity`コマンド、Unityプラグインの`unity:unity-cli`スキル）で、開いているエディタを操作して行う。接続は`unity status`で確認する。プロジェクト側に`com.unity.pipeline`パッケージが必要。Coplay MCPは使わない。
  - PlayをCLIから検証するとき、エディタが最前面にないとゲームが進まない。Play開始後に`unity command eval 'UnityEngine.Application.runInBackground = true; return 1;'`を実行する。この値はプロジェクト設定（`ProjectSettings.asset`の`runInBackground`）に残る。検証が終わったら、編集モードで`UnityEditor.PlayerSettings.runInBackground = false; UnityEditor.AssetDatabase.SaveAssets();`を実行して戻す。画面撮影には`capture_game_view --source screen`を使う。保存先は`Assets/`配下に限られるので、撮影後に画像を`.meta`ごと削除する。
  - `eval`から呼べるのは`public`なメンバーだけ。`internal`は別アセンブリ扱いで見えない。キーやゲームパッドの入力はCLIから送れないので、入力そのものは手で確認する。
  - Play中でないときに`eval`でシーン上のオブジェクトの値を書き換えると、編集中のシーンが変わってしまう。検証用の`eval`は、先頭で`EditorApplication.isPlaying`を確認する。
  - エディタが背面にあると、Playは毎秒約10フレームになる。`WalkPlayer`は`Update`で動くので、歩く、押す、持って回る、が絡む測定は、Play開始後に`UnityEngine.Time.captureFramerate = 60`を設定して行う。設定すると毎秒60フレーム相当で進み、実時間では遅くなる。Playを止めると元に戻る。設定せずに測った「体で押す強さ70」は、実際には何も押せない値だった。
  - スクリプトの初期値を変えても、開いたままのシーンのオブジェクトは、追加された時点の値を持ち続ける。初期値を変えたら、シーンを開き直すか、平面版を作り直してから測る。
- 見た目、反転の動き、衝突結果は、Playでの手動確認が必要。

## アーキテクチャ

asmdefはなく、コードはすべて`Assembly-CSharp`か`Assembly-CSharp-Editor`に入る。

### 3層構造

#### 純粋ルール層

`Assets/PhysicsReversi/Scripts/`にあり、namespaceは`PhysicsReversi`。Unityに依存しないので、`RulesChecks`から直接テストできる。

- `BoardRules`の盤座標は-4〜+4で、1マスが1単位。マスは64個あり、番号は`cell = z*8 + x`。`Recognize`は、石の投影凸包ポリゴンとマスの重なり面積比で石を認識し、`Snapshot`（Ids/Owners配列）を作る。`Captures`は挟み判定だけを行い、盤を変更しない。
- `StoneFaces.Owner(upDot)`は、上面方向との内積から所属を決める。黒=1が+Y、白=2が-Y、横倒しは0。`UpSign(owner)`はその逆で、ある所属の色を見せるにはローカル+Yをどちらへ向けるかを返す。
- `RealtimeCaptures`は、現行Walkモードの捕獲判定。配置イベントではなく「ラインの形成」で発火する。ライン単位のラッチで、同じ並びの再発火と、色反転による振動を防ぐ。`Scan`は、発火した並びの両端の石（`capturingEnds`）も返す。
- `MotionOrigin`は、石の動きの原因を表す。原因は、プレイヤー起因か、反転演出起因かのどちらか。**プレイヤー起因の動きだけが捕獲を起こせる**。捕獲が起きると、同一アクションで動いた石すべての原因を消費済みにする。そのため、反転が新たな捕獲を連鎖させることはない。
- `StoneConfirmation`は、石1個ぶんの「確定した石」の状態を持つ。石は、同じマスに認識され続けると確定し、そのマスから外れ続けると未確定に戻る。別のマスへ移った場合も外れた扱いで、移った先で数え直す。捕獲の反転中は状態を保つ。`ConfirmNow`は、時間を待たずにその場で確定させる。捕獲を起こした並びの両端に使う。
- `StoneHold`は、確定した石の固定の強さを計算する。マスの中心からの距離（マス単位）から0〜1の強さを出し、重さの倍率に直す。
- `StoneThrow`は、石を持っているときの1ボタンの解釈を決める。押した長さから、置くだけ（-1）か、投げの溜め（0〜1）かを決め、溜めから投げる速さを出す。
- `BoardRules.CapturesFrom`は、あるマスに石が着いたら何を挟めるかを返す。石がまだなくても聞ける。`BoardRules.LegalMoves`は、これを使って、ある色の合法手のマスを返す。
- `LegalMoveCaptures`は、合法手のルールの捕獲判定。プレイヤーが動かした未確定の石を、同じマスに待ち時間だけ居続けた時点で1回だけ判定する。待ち時間は`WalkBoardRecognition.confirmSeconds`で、0なら着いた時点で判定する。挟む相手と遠い端は、確定した石だけの盤面（`Settled`）から探す。反転中の石に並びが触れる場合は、反転が終わるまで判定を待つ。採用は未決定で、平面版のシーンで試している（`SPEC.md`の「合法手だけを確定させる案」）。
- `PlacementCaptures`は、配置キュー方式の旧捕獲判定で、全配置を同一スナップショットで評価する。現在は`RulesChecks`からしか呼ばれない。

#### Walk実行層

`Assets/PhysicsReversi/Walk/`にあり、namespaceは`PhysicsReversi.Walk`。本筋のシーンは`Assets/Scenes/PhysicsReversiWalkFlat.unity`で、盤は平面（2026-10-06に決定）。マスがお椀型の`Assets/Scenes/PhysicsReversiWalk.unity`は消さずに残してあるが、使わない。更新も検証もしない。平面版のシーンは、お椀型のシーンの複製から作った。スクリプトは両方のシーンで共有していて、スクリプトの初期値はお椀型のころのまま。平面版のシーンが自分の値を持つ（押す強さ、`allowThrow`、`holdMultiplier`など）。決まっていないルールを試すときは、スイッチを初期状態でオフにしておき、平面版のシーン側で有効にする。

- `WalkBoardRecognition`は、`FixedUpdate`で一定間隔ごとに石のメッシュ頂点を盤平面へ投影し、`BoardRules.Recognize`を呼んで`SnapshotUpdated`イベントを出す。静止判定はない。盤に接地している`OnBoard`の石は、動いていても毎回`CarryStone.ReadUpperFace`で`ownerId`を書き換える。盤の`cellWidth`（ワールド単位）は正規化座標へ変換している。認識のたびに全石の`CarryStone.TickConfirmation`を呼び、確定状態を進める。秒数は`confirmSeconds`と`loosenSeconds`。確定した石のマスは、四隅の印を`confirmedMark`の色に変えて示す。マテリアルは共有のままで、色はMaterialPropertyBlockで上書きする。`holdMultiplier`が1より大きいときは、確定した石の重さを`CarryStone.SetHold`で変える。距離の設定は`fullHoldDistance`と`zeroHoldDistance`。`holdMultiplier`が1より大きいのは、平面版のシーンだけ。`confirmByTime`をオフにすると、時間では確定しなくなり、開始時に盤上にある石だけを最初の認識で確定させる。`Settled`は、確定した石だけを、確定したマスに並べた盤面。合法手の判定、合法手の印、石数表示が読む。
- `WalkCaptureController`は、`SnapshotUpdated`を購読して`RealtimeCaptures.Scan`を実行し、対象の石をRigidbodyのまま物理的に180度回す。色の塗り替えや所属の強制変更はしない。反転中の石は一時的に判定対象から外す。捕獲が起きたら、並びの両端の石を`CarryStone.ConfirmAt`でその場で確定させる（`confirmCapturingStones`）。プレイヤーが未確定の石を持ち直し、同じ石で何度も捕獲することを防ぐため。`captureByLegalMove`をオンにすると、`RealtimeCaptures`の代わりに`LegalMoveCaptures`で判定し、捕獲を起こした石を確定させる。オンなのは平面版のシーンだけ。
- `CarryAuthority`は、掴む・離す（保持と`status`の変更）のすべてが経由する場所。将来のネットワーク権威もここに置く。入力コードから直接書き換えないこと。盤上の所属`ownerId`は、`CarryAuthority`ではなく認識処理が書く。床から落ちた石を予備へ戻す処理（`FixedUpdate` → `ReturnToReserve`）も`CarryAuthority`にある。
  - 掴めるのは、自分の予備石と、未確定の石（色を問わない）。確定した石は掴めない。この範囲は`allowPlacedStonePickup`、`allowOpponentStonePickup`、`lockConfirmedStones`の3スイッチで変えられる（現シーンは3つともオン）。判定の本体は`CarryStone.CanClaim`で、輪郭の強調も同じ判定を使う。
  - 掴んだ石は、持ち主の色が上になるよう回す（`turnHeldStoneToHolderColor`）。`WalkPlayer.Attach`が保持の目標姿勢をその向きに決め、既存の追従モーターが物理的に回す。
  - `allowThrow`がオンのとき、石を持っている間のボタンは、押したときに`TryReady`を、離したときに`TryRelease`を呼ぶ。オンなのは平面版のシーンだけ。短く押せば置く。押し続ければ`WalkPlayer`が石を盤すれすれへ下ろして構え、離すと`Charge`に応じた速さで正面へ投げる。オフのときは、従来どおり`TryReady`がその場で置く。
- `LocalWalkInput`は、1台・1画面で2人を同時に操作する入力。`players[0]`が黒（1台目のゲームパッド、WASD+F）、`players[1]`が白（2台目、IJKL+H）。操作は移動方向と、掴む・離すの1ボタンだけ。移動は固定カメラ`view`を基準にする。照準はなく、正面のいちばん近い掴める石（`Target(index)`）を`CarryAuthority`に渡す。ボタンは、押した時点と離した時点の両方を伝えるだけで、置くか投げるかは`CarryAuthority`が決める。
- `AimHud`は表示専用で、プレイヤーごとに、掴む対象の石の輪郭を強調する。`WalkAssets/SilhouetteHighlight.shader`をマスク、線、塗りの3マテリアルで使い、輪郭の内側に線を描く。
- `LegalMoveMarks`と`ConfirmedStoneMarks`は表示専用で、平面版のシーンの`Board Marks`にある。前者は合法手のマスに、後者は確定した石の上面に、小さな四角を出す。四角のオブジェクトはPlay開始時に作るので、シーンには保存されない。
- `ScoreHud`の`countConfirmedOnly`をオンにすると、石数に確定した石だけを数える。オンなのは平面版のシーンだけ。
- カメラはスクリプトなしの固定で、盤の横から見下ろす。黒が左、白が右。
- `WalkPlayer`は、持った石を動的なRigidbodyのまま、`carryPoint`までの水平距離（腕の長さ）を保って体の周りを回り込ませる。持ち石は`WalkPlayer.carryPoint`（正面）に浮く。目標は「石のいまの方位を、手の方位へ向けて少し回した位置」で、そこへ1ステップで届く速度を与える。以前の「距離 × 8」の追従は、遅れが残り、石が体を横切った。掴んだ直後だけは、従来の追従で手元へ寄せる（`arriving`）。構えた石は`CarryStone.SetCarriedWeight`で軽くし、`readyForce`の上限つきで加速させる。軽いので動き出しが速く、当たりが柔らかい。押す強さは力の上限だけで決まる。石が阻まれて付いてこられないときは、`StayWithStone`がプレイヤーの移動を腕の長さ±0.4に抑える。
- `CarryStone`は、石ごとの状態を持つ。`Owner Id`（上面から決まる盤上の所属）と`Reserve Owner Id`（予備石の持ち主）は別々に管理する。`Confirmed`は確定した石かどうかを表す。`StoneConfirmation`を1つ持ち、離すときと予備へ戻すときにリセットする。石の重さも`CarryStone`が決める。元の重さに、認識処理が渡す固定の強さ（`SetHold`）か、持ち主が渡す軽さ（`SetCarriedWeight`。構えている間だけ）を掛ける。`Rigidbody.mass`を外から直接書き換えないこと。

#### エディタセットアップ層

`Assets/PhysicsReversi/Editor/`にあり、namespaceは`PhysicsReversi.Editor`。シーンは手作業で組まず、`Physics Reversi/Walk/...`メニューのスクリプトで構築する。メニューは、Scene Parts配置、Recognition Rings、Capture Rules、Two-Sided Stones、Capture Practice、Play HUD、Second Player、Score HUD。どのメニューも、Playを止め、`PhysicsReversiWalk`シーンを開いた状態で実行する。既存のオブジェクトがあれば重複して追加しない冪等な作りになっている。生成アセットは`Assets/PhysicsReversi/WalkAssets/`に置く。

これらのメニューは、平面版のシーンでは実行できない（シーン名の確認で止まる）。平面版のシーンに手を入れるメニューを作るときは、`PhysicsReversiWalkFlat`を対象にする。いまあるのはAdd Legal Move Rules（`LegalMoveSetup.cs`）で、合法手のルールのスイッチを入れ、`Board Marks`を足す。

例外はBevel Stone Edgesで、石のメッシュアセットをその場で作り直すだけなので、開いているシーンを問わない。石の見た目には`TwoSidedStone.asset`を使い、当たり判定にはUnity標準の円柱を使う。

平面版は`Create Flat Trial Scene`（`FlatBoardSetup.cs`）が作る。このメニューは、お椀型のシーンを複製し、盤を床の高さへ下ろしてマスの当たり判定を外す。さらに、床に`BoardSurface`を付け、摩擦を`ContactFlat`に替え、プレイヤーの当たりを細くする。`BoardSurface`は、石が盤の面に乗っていることを示す目印。平面版のシーンがすでにあれば、手で調整した値を守るために何もしない。

### シーンの物理（ルールの前提）

盤の形は平面に決まった（`SPEC.md`の「決まっていること」）。摩擦、石の厚さと重さ、プレイヤーの形は、シーンとアセットの作りで、仕様として決めたものではない。数値と測った条件は`README.md`の「平面版のシーン」と「盤と石の物理」にある。測定は2026-10-06にPlayで行った。

平面版のシーン（本筋）:

- 盤は床と同じ高さで、マスに当たり判定はない。石（半径1.3、厚さ0.36、重さ10）は床1枚の上に乗り、置いた位置にそのまま止まる。マスの幅は4なので、中心に置かれた石どうしの隙間は1.4。
- プレイヤーの当たりは細い（半径0.25、`stepOffset`は0.1）。そのため、プレイヤーは石に登れず、3つの手段（体で押す、構えた持ち石、投げ）がどれも起こせる。値は`FlatBoardSetup.cs`がシーンに入れる（押す強さ110、確定石の重さ最大4倍、`allowThrow`オン）。構えの力150と投げの速さ5〜20は、`CarryAuthority`の初期値。測定結果は`README.md`の「平面版で測ったこと」にある。
- 押す強さ110は、固定されていない石だけが動く値。確定した石は、体では押しのけられない。
- 重力は14で、標準の9.81とは違う。滑る石の減速は「摩擦係数 × 重力」の約2.2倍だった。力や速さの初期値は計算で決めず、Playで測って決める。
- 捕獲の反転（`CarryStone.FlipBody`）は速度を直接書き換えるので、反転する石に乗り上げた石を跳ね飛ばす。測ったのはお椀型のシーンで、平面版では確かめていない。

お椀型のシーン（使わない。平面に決めた理由の記録）:

- マスはお椀型（深さ0.45）で、摩擦が低い（動0.04、静0.1）。石は、お椀に収まると盤の面から0.14しか出ない。
- プレイヤーの`CharacterController`は半径0.38で、`stepOffset`は0.6。石の上面がこの両方より低いと、プレイヤーは石を押さずに乗り上げて越える。押す処理（`WalkPlayer.OnControllerColliderHit`）はあるが、測定では石は動かなかった。
- 持ち石は足元から1.6上（`carryPoint`）にあり、置かれた石の0.86以上、上を通る。そのため、「持ち石がぶつかった石」の扱い（`CarryStone.TrackMotion`）は、置かれた石に対してはまず起きない。
- この結果、お椀に収まって確定した石は、掴めず、押せず、持ち石も当てられなかった。

### 旧プロトタイプ

`Assets/PhysicsReversi/Prototype.unity`、`PrototypeGame.cs`、`TuningConfig`（`Tuning.asset`）は、ランチャーで石を撃つ初期版。旧プロトタイプも`BoardRules`を使うので、ルール層を変更するときは、Walk版と旧プロトタイプの両方への影響を考慮する。

## 未実装（READMEより）

手番、対局終了、微振動の強制収束、通信同期は未実装。オンラインは当面の目標外。盤は平面に決まった。確定した石の動きにくさと、確定した石を崩す手段は、平面版のシーンにあり、値は調整中。黒(1)と白(2)の2人が、1台のPCで同時に操作する。
