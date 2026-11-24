#!/bin/bash
# Edit/Write の前に実行される。
# Unity がシリアライズするファイルへの直接編集を止める。
# jq がない環境では止めずに通す（規則は CLAUDE.md にもある）。
command -v jq >/dev/null 2>&1 || exit 0
INPUT=$(cat)
F=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
F="${F//\\//}"
case "$F" in
  *.unity|*.prefab|*.asset|*.mat|*.meta|\
  *.anim|*.controller|*.physicMaterial|\
  */ProjectSettings/*|*/Library/*)
    echo "ブロック: $F は Unity が管理するファイルです。" \
      "unity command で Editor 経由で変更してください。" >&2
    exit 2 ;;
esac
exit 0
