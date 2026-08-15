import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import {
  convertMermaidToFeishuBoard,
  parseMermaidDiagram,
} from './convert_mermaid_to_feishu_board.mjs';

const sourceDir = path.resolve(process.argv[2] ?? 'Docs/design');
const outputDir = path.resolve(process.argv[3] ?? 'artifacts/feishu-board-audit');
const records = [];

for (const filePath of listMarkdownFiles(sourceDir)) {
  const relativePath = path.relative(sourceDir, filePath).replaceAll('\\', '/');
  const content = fs.readFileSync(filePath, 'utf8');
  let diagramIndex = 0;
  for (const match of content.matchAll(/```mermaid\s*\r?\n([\s\S]*?)```/g)) {
    diagramIndex++;
    const source = match[1].trim();
    const line = content.slice(0, match.index).split(/\r?\n/).length;
    const declaredType = source.match(/^\s*([^\s]+)/)?.[1] ?? 'unknown';
    const record = {
      file: relativePath,
      line,
      diagram: diagramIndex,
      declaredType,
      status: 'passed',
      boardType: null,
      nodeCount: 0,
      error: null,
    };
    try {
      const board = await convertMermaidToFeishuBoard(source);
      validateBoard(board);
      const diagram = await parseMermaidDiagram(source);
      validateSemantics(diagram, board);
      record.boardType = board.diagramType;
      record.nodeCount = board.nodes.length;
    } catch (error) {
      record.status = 'failed';
      record.error = String(error?.message ?? error);
    }
    records.push(record);
  }
}

const passed = records.filter((record) => record.status === 'passed').length;
const failures = records.filter((record) => record.status === 'failed');
const summary = {
  sourceDir: path.relative(process.cwd(), sourceDir).replaceAll('\\', '/'),
  total: records.length,
  passed,
  failed: failures.length,
  passRate: records.length === 0 ? 0 : Number((passed / records.length).toFixed(4)),
  byType: countBy(records, (record) => record.declaredType),
  failuresByReason: countBy(failures, (record) => record.error),
};

fs.mkdirSync(outputDir, { recursive: true });
fs.writeFileSync(
  path.join(outputDir, 'report.json'),
  `${JSON.stringify({ summary, records }, null, 2)}\n`,
  'utf8',
);
fs.writeFileSync(path.join(outputDir, 'report.md'), markdownReport(summary, failures), 'utf8');
console.log(`Board conversion audit: ${passed}/${records.length} passed (${(summary.passRate * 100).toFixed(2)}%)`);
console.log(`Report: ${path.relative(process.cwd(), path.join(outputDir, 'report.md'))}`);
process.exitCode = failures.length === 0 ? 0 : 2;

function listMarkdownFiles(directory) {
  const files = [];
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) files.push(...listMarkdownFiles(fullPath));
    else if (entry.isFile() && entry.name.endsWith('.md')) files.push(fullPath);
  }
  return files.sort((left, right) => left.localeCompare(right));
}

function validateBoard(board) {
  if (!board || board.schemaVersion !== 1 || !board.diagramType) {
    throw new Error('Converter returned an invalid Board document header.');
  }
  if (!Number.isFinite(board.width) || !Number.isFinite(board.height) || board.width <= 0 || board.height <= 0) {
    throw new Error('Converter returned invalid Board dimensions.');
  }
  if (!Array.isArray(board.nodes) || board.nodes.length === 0) {
    throw new Error('Converter returned no Board nodes.');
  }
  const ids = new Set();
  for (const node of board.nodes) {
    if (!node.id || !node.type) throw new Error('Board node is missing id or type.');
    if (ids.has(node.id)) throw new Error(`Duplicate Board node id: ${node.id}`);
    ids.add(node.id);
  }
}

function validateSemantics(diagram, board) {
  if (diagram.type === 'flowchart-v2') {
    const expected = diagram.db.getSubGraphs?.().length ?? 0;
    if (board.semantics?.subgraphCount !== expected) {
      throw new Error(`Flowchart subgraph mismatch: expected ${expected}, got ${board.semantics?.subgraphCount ?? 0}.`);
    }
    return;
  }
  if (diagram.type === 'sequence') {
    const messages = diagram.db.getMessages();
    const expectedNotes = messages.filter((message) => message.type === 2).length;
    const expectedAutoNumber = messages.some((message) => message.type === 26);
    if (board.semantics?.noteCount !== expectedNotes || board.semantics?.autoNumber !== expectedAutoNumber) {
      throw new Error('Sequence Note or autonumber semantics were not preserved.');
    }
    return;
  }
  if (diagram.type === 'stateDiagram' || diagram.type === 'class') {
    const expectedNodes = diagram.type === 'stateDiagram'
      ? diagram.db.getStates().size
      : diagram.db.getClasses().size;
    const expectedEdges = diagram.db.getRelations().length;
    if (board.semantics?.nodeCount !== expectedNodes || board.semantics?.edgeCount !== expectedEdges) {
      throw new Error(`${diagram.type} node or relation semantics were not preserved.`);
    }
    return;
  }
  if (diagram.type === 'mindmap') {
    const countTree = (node) => 1 + (node.children ?? []).reduce((sum, child) => sum + countTree(child), 0);
    const expected = countTree(diagram.db.getMindmap());
    if (board.semantics?.mindmapNodeCount !== expected) {
      throw new Error(`Mind map node mismatch: expected ${expected}, got ${board.semantics?.mindmapNodeCount ?? 0}.`);
    }
  }
}

function countBy(items, selector) {
  return Object.fromEntries(
    Array.from(items.reduce((counts, item) => {
      const key = selector(item) ?? 'unknown';
      counts.set(key, (counts.get(key) ?? 0) + 1);
      return counts;
    }, new Map())).sort(([left], [right]) => left.localeCompare(right)),
  );
}

function markdownReport(summary, failures) {
  const lines = [
    '# Feishu Board conversion audit',
    '',
    `SourceDir: ${summary.sourceDir}`,
    `Total: ${summary.total}`,
    `Passed: ${summary.passed}`,
    `Failed: ${summary.failed}`,
    `PassRate: ${(summary.passRate * 100).toFixed(2)}%`,
    '',
    '## Diagram types',
    '',
    '| Type | Count |',
    '|------|------:|',
    ...Object.entries(summary.byType).map(([type, count]) => `| ${escapeCell(type)} | ${count} |`),
    '',
    '## Failures',
    '',
    '| File | Line | Diagram | Type | Error |',
    '|------|-----:|--------:|------|-------|',
  ];
  if (failures.length === 0) lines.push('| - | - | - | - | None |');
  else {
    for (const failure of failures) {
      lines.push(`| ${escapeCell(failure.file)} | ${failure.line} | ${failure.diagram} | ${escapeCell(failure.declaredType)} | ${escapeCell(failure.error)} |`);
    }
  }
  lines.push('');
  return `${lines.join('\n')}\n`;
}

function escapeCell(value) {
  return String(value).replaceAll('|', '\\|').replaceAll('\r', ' ').replaceAll('\n', ' ');
}
