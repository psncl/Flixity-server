import { tmpdir } from "node:os";
import path from "node:path";

// One marker file per Claude Code session, in the OS temp folder (never in the repo).
export function markerPath(sessionId) {
  const safeId = String(sessionId ?? "unknown").replace(/[^A-Za-z0-9_-]/g, "");
  return path.join(tmpdir(), `flixity-claude-code-changed-${safeId}`);
}
