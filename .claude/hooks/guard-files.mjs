// PreToolUse hook for Edit/Write/MultiEdit.
// - Denies edits to archived OpenSpec changes (they are history).
// - Asks before hand edits to EF Core migrations (they should come from `dotnet ef`).
// - Denies writing anything that looks like a secret (secrets go in user secrets).
import { readFileSync } from "node:fs";
import path from "node:path";

const input = JSON.parse(readFileSync(0, "utf8").trimStart());
const projectDir = process.env.CLAUDE_PROJECT_DIR ?? input.cwd ?? process.cwd();
const toolInput = input.tool_input ?? {};
const filePath = toolInput.file_path ?? "";
const rel = path.relative(projectDir, path.resolve(projectDir, filePath)).replaceAll("\\", "/");

function decide(decision, reason) {
  console.log(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      permissionDecision: decision,
      permissionDecisionReason: reason,
    },
  }));
  process.exit(0);
}

if (rel.startsWith("openspec/changes/archive/")) {
  decide("deny", "Archived OpenSpec changes are history and must not be edited. Change openspec/specs/ or open a new change instead.");
}

// Text the tool is about to write.
const newText = [
  toolInput.content,
  toolInput.new_string,
  ...(toolInput.edits ?? []).map((e) => e.new_string),
].filter(Boolean).join("\n");

const secretPatterns = [
  /\bsk-[A-Za-z0-9_-]{20,}/, // OpenAI-style keys
  /\bgh[pousr]_[A-Za-z0-9]{30,}/, // GitHub tokens
  /-----BEGIN [A-Z ]*PRIVATE KEY-----/,
];
if (/^src\/appsettings[^/]*\.json$/i.test(rel)) {
  // In config files, any non-empty value for a key/secret/password/token setting.
  secretPatterns.push(/"[A-Za-z]*(ApiKey|Key|Secret|Password|Token)"\s*:\s*"[^"]+"/i);
  secretPatterns.push(/Password=[^;"]+/i);
}
if (secretPatterns.some((p) => p.test(newText))) {
  decide("deny", `This edit to ${rel} looks like it contains a secret. Store secrets with \`dotnet user-secrets set\` (see README.md) and keep only non-secret settings in files.`);
}

if (/^src\/Common\/Database\/Migrations\//.test(rel)) {
  decide("ask", "Migrations are normally generated with `dotnet ef migrations add`. Allow this hand edit?");
}

process.exit(0);
