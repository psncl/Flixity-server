// PostToolUse hook for Edit/Write/MultiEdit: remembers that C# code changed this
// session, so the Stop hook only builds and tests when there is something to check.
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { markerPath } from "./marker.mjs";

const input = JSON.parse(readFileSync(0, "utf8").trimStart());
const filePath = input.tool_input?.file_path ?? "";

if (/(\.(cs|csproj|props|targets|sln)|appsettings[^\\/]*\.json)$/i.test(filePath)) {
  const marker = markerPath(input.session_id);
  // The file holds how many times the Stop hook has blocked; keep it if present.
  if (!existsSync(marker)) writeFileSync(marker, "0");
}
