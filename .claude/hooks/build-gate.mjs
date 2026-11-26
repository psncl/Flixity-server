// Stop hook: if C# changed this session, build the solution and run the unit tests
// before Claude may finish. On failure it exits with code 2, which blocks the stop and
// sends the errors back to Claude to fix. Integration tests are left out: they need
// Docker and are slow.
import { spawnSync } from "node:child_process";
import { existsSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { markerPath } from "./marker.mjs";

const MAX_BLOCKS = 3; // stop blocking after this many failed attempts in a row

const input = JSON.parse(readFileSync(0, "utf8").trimStart());
const projectDir = process.env.CLAUDE_PROJECT_DIR ?? input.cwd ?? process.cwd();
const marker = markerPath(input.session_id);

if (!existsSync(marker)) process.exit(0); // no code changes since the last green run

function run(args) {
  const result = spawnSync("dotnet", args, { cwd: projectDir, encoding: "utf8", shell: false });
  return { ok: result.status === 0, output: `${result.stdout ?? ""}${result.stderr ?? ""}` };
}

function fail(step, output, pickLines) {
  const blocks = Number(readFileSync(marker, "utf8")) + 1;
  if (blocks > MAX_BLOCKS) {
    // Give up blocking so Claude can report the problem instead of looping.
    rmSync(marker, { force: true });
    console.log(JSON.stringify({ systemMessage: `Build gate: ${step} still failing after ${MAX_BLOCKS} attempts; not blocking any more.` }));
    process.exit(0);
  }
  writeFileSync(marker, String(blocks));
  const lines = output.split(/\r?\n/).filter(pickLines);
  const details = [...new Set(lines)].slice(0, 40).join("\n").slice(0, 6000);
  console.error(`Build gate: ${step} failed (attempt ${blocks}/${MAX_BLOCKS}). Fix this before finishing:\n${details}`);
  process.exit(2);
}

const build = run(["build", "src/server.sln", "-nologo", "-v", "q", "-clp:NoSummary"]);
if (!build.ok) fail("dotnet build", build.output, (l) => /error|locked|being used by another process/i.test(l));

const test = run(["test", "tests/UnitTests", "--no-build", "--nologo"]);
if (!test.ok) fail("unit tests", test.output, (l) => /fail|error|assert|expected|but found|^\s+at /i.test(l));

rmSync(marker, { force: true });
