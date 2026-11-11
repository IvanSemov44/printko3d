// Claude Code hooks for this repository (decision 0021).
// Usage: node guard.mjs <protect|format|remind>; the hook JSON arrives on stdin.
//   protect: PreToolUse on Edit/Write. Blocks edits of generated and sensitive files (exit 2).
//   format:  PostToolUse on Edit/Write. Formats the changed file and remembers that code changed.
//   remind:  Stop. Once per run of work, reminds to run build and tests if code changed.
// A hook guards against mistakes, not against intent: writes made through Bash are not seen here.
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

const mode = process.argv[2];
const input = JSON.parse(fs.readFileSync(0, "utf8") || "{}");
const root = path.resolve(
  process.env.CLAUDE_PROJECT_DIR ?? input.cwd ?? process.cwd(),
);
const filePath = input.tool_input?.file_path;
const rel = filePath
  ? path.relative(root, path.resolve(root, filePath)).split(path.sep).join("/")
  : "";
const insideRepo = rel !== "" && !rel.startsWith("..") && !path.isAbsolute(rel);
const marker = path.join(
  os.tmpdir(),
  `printko-claude-code-changed-${input.session_id ?? "unknown"}`,
);

function git(args) {
  return spawnSync("git", args, { cwd: root, encoding: "utf8" });
}

function existsOnMain(repoPath) {
  return ["origin/main", "main"].some(
    (ref) => git(["cat-file", "-e", `${ref}:${repoPath}`]).status === 0,
  );
}

function protect() {
  if (!insideRepo) return;
  const name = path.posix.basename(rel);
  let reason = "";
  if (rel === "src/web/shared/src/api/schema.d.ts") {
    reason =
      "schema.d.ts is generated from openapi.json. Run `npm run api:types` in src/web/shared instead.";
  } else if (
    (name === ".env" || name.startsWith(".env.")) &&
    name !== ".env.example"
  ) {
    reason = `${name} holds secrets and is never edited by the AI. Ask the human to change it.`;
  } else if (/(^|\/)Persistence\/Migrations\//.test(rel) && existsOnMain(rel)) {
    reason =
      "This migration is already merged into main. Do not edit it; add a new migration instead.";
  }
  if (reason) {
    process.stderr.write(`Blocked by .claude/hooks/guard.mjs: ${reason}\n`);
    process.exit(2);
  }
}

function format() {
  if (!insideRepo || !fs.existsSync(path.resolve(root, rel))) return;
  let result;
  if (rel.endsWith(".cs")) {
    result = spawnSync(
      "dotnet",
      ["format", "Storefront.slnx", "--no-restore", "--include", rel],
      { cwd: root, encoding: "utf8" },
    );
  } else if (/\.tsx?$/.test(rel) && rel.startsWith("src/web/")) {
    result = spawnSync(
      process.execPath,
      [
        path.join(root, "src/web/node_modules/prettier/bin/prettier.cjs"),
        "--write",
        path.resolve(root, rel),
      ],
      { cwd: path.join(root, "src/web"), encoding: "utf8" },
    );
  } else {
    return;
  }
  fs.writeFileSync(marker, rel);
  if (result.status !== 0) {
    // Exit 1 is a non-blocking error: the edit stays, the message is shown.
    process.stderr.write(
      `Formatting failed for ${rel}:\n${result.stdout}${result.stderr}`,
    );
    process.exit(1);
  }
}

function remind() {
  if (input.stop_hook_active || !fs.existsSync(marker)) return;
  fs.rmSync(marker, { force: true });
  const reason =
    'Code was changed in this session. Before finishing, run the checks from CLAUDE.md, section "Команди" ' +
    "(dotnet build and dotnet test for backend; npm run lint, npm run format:check and npm test in src/web for frontend) " +
    "and report the real result, or say which checks you did not run and why.";
  process.stdout.write(JSON.stringify({ decision: "block", reason }));
}

({ protect, format, remind })[mode]?.();
