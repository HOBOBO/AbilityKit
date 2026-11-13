const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const Ajv2019 = require("ajv/dist/2019").default;
const addFormats = require("ajv-formats");

const repoRoot = path.resolve(__dirname, "..", "..");
const schemaRoot = path.join(
  repoRoot,
  "Unity",
  "Packages",
  "com.abilitykit.battlescenario",
  "Schemas",
);
const examplesRoot = path.join(schemaRoot, "examples");
const schema = JSON.parse(
  fs.readFileSync(path.join(schemaRoot, "artifact-v1.schema.json"), "utf8"),
);
const ajv = new Ajv2019({ allErrors: true, strict: true, allowUnionTypes: true });
addFormats(ajv);
const validateArtifact = ajv.compile(schema);
const validateTraceRecord = ajv.compile(schema.$defs.traceRecord);

function readJson(name) {
  return JSON.parse(fs.readFileSync(path.join(examplesRoot, name), "utf8"));
}

test("all JSON examples conform to one of the v1 artifact contracts", () => {
  for (const name of ["run-manifest.json", "case-summary.json", "batch-summary.json"]) {
    const value = readJson(name);
    assert.equal(validateArtifact(value), true, `${name}: ${ajv.errorsText(validateArtifact.errors)}`);
  }
});

test("every JSONL trace line conforms to the trace record contract", () => {
  const lines = fs
    .readFileSync(path.join(examplesRoot, "trace-record.jsonl"), "utf8")
    .split(/\r?\n/)
    .filter((line) => line.length > 0);
  assert.ok(lines.length > 0, "trace example must contain at least one record");

  for (const [index, line] of lines.entries()) {
    const record = JSON.parse(line);
    assert.equal(
      validateTraceRecord(record),
      true,
      `trace-record.jsonl:${index + 1}: ${ajv.errorsText(validateTraceRecord.errors)}`,
    );
  }
});

test("v1 rejects missing identity, invalid status, and a different major version", () => {
  const valid = readJson("case-summary.json");

  const missingIdentity = { ...valid };
  delete missingIdentity.runId;
  assert.equal(validateArtifact(missingIdentity), false);

  const invalidStatus = { ...valid, status: "almost-passed" };
  assert.equal(validateArtifact(invalidStatus), false);

  const futureMajor = { ...valid, schemaVersion: 2 };
  assert.equal(validateArtifact(futureMajor), false);

  const invalidTimestamp = { ...valid, startedAt: "not-a-date" };
  assert.equal(validateArtifact(invalidTimestamp), false);
});

test("v1 consumers can ignore fields appended at any object level", () => {
  const value = readJson("case-summary.json");
  value.futureOptionalField = { producerSpecific: true };
  value.build.futureBuildMetadata = "ignored-by-v1-consumers";
  value.extensions["org.example.future"] = { revision: 2 };

  assert.equal(validateArtifact(value), true, ajv.errorsText(validateArtifact.errors));
});
