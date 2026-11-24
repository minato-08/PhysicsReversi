#!/bin/bash
# Claude が応答を終える前に実行される。
# C# に未コミットの変更があるときだけコンパイルを確認する。
# Editor に接続できない（終了コード 7）ときは止めない。
command -v jq >/dev/null 2>&1 || exit 0
INPUT=$(cat)
ACTIVE=$(echo "$INPUT" | jq -r '.stop_hook_active')
[ "$ACTIVE" = "true" ] && exit 0
cd "$CLAUDE_PROJECT_DIR" || exit 0
CHANGED=$(git status --porcelain -- '*.cs' '*.asmdef' '*.asmref')
[ -z "$CHANGED" ] && exit 0
# macOS では CLI が PATH にないことがある。
UNITY=$(command -v unity || echo "$HOME/.unity/bin/unity")
[ -x "$UNITY" ] || exit 0
OUT=$("$UNITY" recompile --project-path "$CLAUDE_PROJECT_DIR" --format json 2>&1)
CODE=$?
if [ $CODE -eq 6 ]; then
  echo "コンパイルエラーがあります。修正してから終えてください:" >&2
  echo "$OUT" >&2
  exit 2
fi
exit 0
