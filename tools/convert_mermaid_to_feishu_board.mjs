import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const validationPackage = path.resolve(
  process.cwd(),
  'artifacts/mermaid-validation/package.json',
);
const validationRequire = createRequire(pathToFileURL(validationPackage).href);
const { JSDOM } = validationRequire('jsdom');
const dom = new JSDOM('<!doctype html><html><body></body></html>');
globalThis.window = dom.window;
globalThis.document = dom.window.document;
globalThis.DOMParser = dom.window.DOMParser;
globalThis.Element = dom.window.Element;
Object.defineProperty(globalThis, 'navigator', {
  configurable: true,
  value: dom.window.navigator,
});

const mermaidEntry = path.resolve(
  process.cwd(),
  'artifacts/mermaid-validation/node_modules/mermaid/dist/mermaid.esm.mjs',
);
if (!fs.existsSync(mermaidEntry)) {
  throw new Error(
    'Mermaid dependency missing. Run: npm install --prefix artifacts\\mermaid-validation mermaid jsdom',
  );
}
const mermaid = (await import(pathToFileURL(mermaidEntry).href)).default;
mermaid.initialize({
  startOnLoad: false,
  securityLevel: 'loose',
  flowchart: { htmlLabels: false },
  sequence: { useMaxWidth: false },
});

export async function parseMermaidDiagram(source) {
  return mermaid.mermaidAPI.getDiagramFromText(source.trim());
}

export async function convertMermaidToFeishuBoard(source) {
  const diagram = await parseMermaidDiagram(source);
  const result = diagram.type === 'flowchart-v2'
    ? convertFlowchart(diagram.db)
    : diagram.type === 'sequence'
      ? convertSequence(diagram.db)
      : diagram.type === 'stateDiagram'
        ? convertStateDiagram(diagram.db)
        : diagram.type === 'class'
          ? convertClassDiagram(diagram.db)
          : diagram.type === 'mindmap'
            ? convertMindmap(diagram.db)
            : null;

  if (!result) {
    throw new Error(
      `Unsupported Mermaid diagram type for editable Feishu board: ${diagram.type}`,
    );
  }
  return result;
}

const invokedPath = process.argv[1] ? path.resolve(process.argv[1]) : '';
if (invokedPath === fileURLToPath(import.meta.url)) {
  const inputPath = process.argv[2];
  const outputPath = process.argv[3];
  if (!inputPath || !outputPath) {
    throw new Error(
      'Usage: node tools/convert_mermaid_to_feishu_board.mjs <input.mmd> <output.json>',
    );
  }

  const source = fs.readFileSync(path.resolve(inputPath), 'utf8');
  const result = await convertMermaidToFeishuBoard(source);
  const resolvedOutput = path.resolve(outputPath);
  fs.mkdirSync(path.dirname(resolvedOutput), { recursive: true });
  fs.writeFileSync(resolvedOutput, `${JSON.stringify(result, null, 2)}\n`, 'utf8');
}

function convertFlowchart(db) {
  const vertices = Array.from(db.getVertices().values());
  const edges = db.getEdges();
  if (vertices.length === 0) {
    throw new Error('Flowchart contains no vertices.');
  }

  const direction = String(db.getDirection?.() ?? 'TB').toUpperCase();
  const horizontal = direction === 'LR' || direction === 'RL';
  const reversed = direction === 'RL' || direction === 'BT';
  const rankById = calculateRanks(vertices, edges);
  const ranks = new Map();
  for (const vertex of vertices) {
    const rank = rankById.get(vertex.id) ?? 0;
    if (!ranks.has(rank)) ranks.set(rank, []);
    ranks.get(rank).push(vertex);
  }

  const rankKeys = Array.from(ranks.keys()).sort((a, b) => a - b);
  const nodeWidth = 220;
  const nodeHeight = 84;
  const rankGap = horizontal ? 150 : 110;
  const laneGap = horizontal ? 70 : 80;
  const margin = 80;
  const maxLaneCount = Math.max(...Array.from(ranks.values(), (items) => items.length));
  const positions = new Map();

  for (const rank of rankKeys) {
    const items = ranks.get(rank).sort((a, b) => a.id.localeCompare(b.id));
    const rankIndex = reversed ? rankKeys.length - 1 - rank : rank;
    for (let lane = 0; lane < items.length; lane++) {
      const laneOffset = ((maxLaneCount - items.length) * (horizontal ? nodeHeight + laneGap : nodeWidth + laneGap)) / 2;
      const x = horizontal
        ? margin + rankIndex * (nodeWidth + rankGap)
        : margin + laneOffset + lane * (nodeWidth + laneGap);
      const y = horizontal
        ? margin + laneOffset + lane * (nodeHeight + laneGap)
        : margin + rankIndex * (nodeHeight + rankGap);
      positions.set(items[lane].id, { x, y });
    }
  }

  const shapeNodes = vertices.map((vertex, index) => {
    const position = positions.get(vertex.id);
    return {
      id: nodeId('node', index),
      mermaidId: vertex.id,
      type: 'composite_shape',
      x: position.x,
      y: position.y,
      width: nodeWidth,
      height: nodeHeight,
      text: boardText(cleanText(vertex.text || vertex.id), 16, 'bold'),
      style: shapeStyle(vertex.type),
      composite_shape: { type: boardShape(vertex.type) },
      z_index: 2,
    };
  });
  const boardIdByMermaidId = new Map(
    shapeNodes.map((node) => [node.mermaidId, node.id]),
  );
  for (const node of shapeNodes) delete node.mermaidId;

  const connectors = edges.map((edge, index) => {
    const from = boardIdByMermaidId.get(edge.start);
    const to = boardIdByMermaidId.get(edge.end);
    if (!from || !to) {
      throw new Error(`Flowchart edge references an unknown vertex: ${edge.start} -> ${edge.end}`);
    }
    const dotted = edge.stroke === 'dotted';
    const thick = edge.stroke === 'thick';
    return {
      id: nodeId('edge', index),
      type: 'connector',
      style: {
        border_color: '#475569',
        border_style: dotted ? 'dash' : 'solid',
        border_width: thick ? 'bold' : 'narrow',
      },
      connector: {
        start: {
          attached_object: { id: from, snap_to: horizontal ? 'right' : 'bottom' },
          arrow_style: 'none',
        },
        end: {
          attached_object: { id: to, snap_to: horizontal ? 'left' : 'top' },
          arrow_style: edge.type === 'arrow_open' ? 'line_arrow' : 'triangle_arrow',
        },
        captions: edge.text
          ? { data: [boardText(cleanText(edge.text), 14, 'regular')] }
          : undefined,
        shape: 'right_angled_polyline',
        caption_auto_direction: true,
      },
      z_index: 1,
    };
  });

  const groups = (db.getSubGraphs?.() ?? []).map((group, index) => {
    const members = group.nodes.map((id) => positions.get(id)).filter(Boolean);
    if (members.length === 0) {
      throw new Error(`Flowchart subgraph contains no known vertices: ${group.id}`);
    }
    const paddingX = 34;
    const paddingTop = 50;
    const paddingBottom = 28;
    const minX = Math.min(...members.map((item) => item.x)) - paddingX;
    const minY = Math.min(...members.map((item) => item.y)) - paddingTop;
    const maxX = Math.max(...members.map((item) => item.x + nodeWidth)) + paddingX;
    const maxY = Math.max(...members.map((item) => item.y + nodeHeight)) + paddingBottom;
    return {
      id: nodeId('group', index),
      type: 'composite_shape',
      x: minX,
      y: minY,
      width: maxX - minX,
      height: maxY - minY,
      text: {
        ...boardText(cleanText(group.title || group.id), 14, 'bold'),
        horizontal_align: 'left',
        vertical_align: 'top',
      },
      style: {
        fill_color: '#F8FAFC',
        fill_opacity: 0.18,
        border_color: '#94A3B8',
        border_style: 'dash',
        border_width: 'narrow',
      },
      composite_shape: { type: 'rect' },
      z_index: 0,
    };
  });

  const allBounds = [...Array.from(positions.values()), ...groups];
  const maxX = Math.max(...allBounds.map((item) => item.x + (item.width ?? nodeWidth)));
  const maxY = Math.max(...allBounds.map((item) => item.y + (item.height ?? nodeHeight)));
  return {
    schemaVersion: 1,
    diagramType: 'flowchart',
    width: Math.max(800, maxX + margin),
    height: Math.max(480, maxY + margin),
    nodes: [...groups, ...shapeNodes, ...connectors],
    semantics: { subgraphCount: groups.length },
  };
}

function convertSequence(db) {
  const actors = Array.from(db.getActors().values());
  const messages = db.getMessages();
  if (actors.length === 0) {
    throw new Error('Sequence diagram contains no actors.');
  }

  const actorWidth = 190;
  const actorHeight = 64;
  const actorGap = 150;
  const marginX = 80;
  const top = 50;
  const messageStartY = 155;
  const messageGap = 78;
  const bottomPadding = 100;
  const lifelineEndY = messageStartY + Math.max(1, messages.length) * messageGap + 30;
  const actorId = new Map();
  const actorCenter = new Map();
  const nodes = [];

  actors.forEach((actor, index) => {
    const x = marginX + index * (actorWidth + actorGap);
    const id = nodeId('actor', index);
    actorId.set(actor.name, id);
    actorCenter.set(actor.name, x + actorWidth / 2);
    nodes.push({
      id,
      type: 'composite_shape',
      x,
      y: top,
      width: actorWidth,
      height: actorHeight,
      text: boardText(cleanText(actor.description || actor.name), 16, 'bold'),
      style: {
        fill_color: '#EFF6FF',
        fill_opacity: 1,
        border_color: '#2563EB',
        border_style: 'solid',
        border_width: 'narrow',
      },
      composite_shape: { type: actor.type === 'actor' ? 'actor' : 'round_rect2' },
      z_index: 3,
    });
    nodes.push({
      id: nodeId('life', index),
      type: 'connector',
      style: {
        border_color: '#94A3B8',
        border_style: 'dash',
        border_width: 'extra_narrow',
      },
      connector: {
        start: { position: { x: x + actorWidth / 2, y: top + actorHeight }, arrow_style: 'none' },
        end: { position: { x: x + actorWidth / 2, y: lifelineEndY }, arrow_style: 'none' },
        shape: 'straight',
        specified_coordinate: true,
      },
      z_index: 1,
    });
  });

  const controlStarts = new Map([
    [10, 'loop'],
    [12, 'alt'],
    [15, 'opt'],
  ]);
  const controlEnds = new Set([11, 14, 16]);
  const controlStack = [];
  const controlFrames = [];
  const controlSeparators = [];
  let visualMessageIndex = 0;
  let autoNumber = false;
  let numberedMessageCount = 0;
  let noteCount = 0;

  messages.forEach((message, index) => {
    if (message.type === 26) {
      autoNumber = true;
      return;
    }
    if (controlStarts.has(message.type)) {
      controlStack.push({
        kind: controlStarts.get(message.type),
        label: cleanText(message.message),
        start: visualMessageIndex,
        separators: [],
      });
      return;
    }
    if (message.type === 13) {
      if (controlStack.length === 0 || controlStack[controlStack.length - 1].kind !== 'alt') {
        throw new Error('Sequence diagram contains an else record outside an alt fragment.');
      }
      controlStack[controlStack.length - 1].separators.push({
        index: visualMessageIndex,
        label: cleanText(message.message),
      });
      return;
    }
    if (controlEnds.has(message.type)) {
      if (controlStack.length === 0) {
        throw new Error(`Sequence diagram contains unmatched control end type ${message.type}.`);
      }
      const frame = controlStack.pop();
      frame.end = visualMessageIndex;
      controlFrames.push(frame);
      return;
    }
    if (message.type === 2) {
      const participantNames = [message.from, message.to].filter((name) => actorCenter.has(name));
      if (participantNames.length === 0) {
        throw new Error('Sequence Note does not reference a known actor.');
      }
      const y = messageStartY + visualMessageIndex * messageGap - 24;
      visualMessageIndex++;
      noteCount++;
      const centers = participantNames.map((name) => actorCenter.get(name));
      const centerX = centers.reduce((sum, value) => sum + value, 0) / centers.length;
      const noteWidth = Math.max(180, Math.abs(Math.max(...centers) - Math.min(...centers)) + 160);
      nodes.push({
        id: nodeId('note', index),
        type: 'composite_shape',
        x: centerX - noteWidth / 2,
        y,
        width: noteWidth,
        height: 56,
        text: boardText(cleanText(message.message), 14, 'regular'),
        style: {
          fill_color: '#FEF3C7',
          fill_opacity: 1,
          border_color: '#D97706',
          border_style: 'solid',
          border_width: 'narrow',
        },
        composite_shape: { type: 'rect' },
        z_index: 3,
      });
      return;
    }
    if (!message.from || !message.to || !actorCenter.has(message.from) || !actorCenter.has(message.to)) {
      throw new Error(`Sequence diagram contains unsupported message type ${message.type}.`);
    }

    const y = messageStartY + visualMessageIndex * messageGap;
    visualMessageIndex++;
    numberedMessageCount++;
    const response = message.type === 1 || message.type === 4 || message.type === 6;
    const arrowStyle = message.type === 3 ? 'line_arrow' : 'triangle_arrow';
    const messageText = cleanText(message.message);
    const caption = autoNumber ? `${numberedMessageCount}. ${messageText}` : messageText;
    nodes.push({
      id: nodeId('message', index),
      type: 'connector',
      style: {
        border_color: response ? '#64748B' : '#0F172A',
        border_style: response ? 'dash' : 'solid',
        border_width: 'narrow',
      },
      connector: {
        start: {
          position: { x: actorCenter.get(message.from), y },
          arrow_style: 'none',
        },
        end: {
          position: { x: actorCenter.get(message.to), y },
          arrow_style: arrowStyle,
        },
        captions: caption
          ? { data: [boardText(caption, 14, 'regular')] }
          : undefined,
        shape: message.from === message.to ? 'curve' : 'straight',
        caption_auto_direction: true,
        specified_coordinate: true,
      },
      z_index: 2,
    });
  });

  if (controlStack.length > 0) {
    throw new Error(`Sequence diagram contains an unclosed ${controlStack[controlStack.length - 1].kind} fragment.`);
  }

  const frameX = marginX - 30;
  const frameWidth = actors.length * actorWidth + (actors.length - 1) * actorGap + 60;
  controlFrames.forEach((frame, index) => {
    const startY = messageStartY + frame.start * messageGap - 42;
    const endY = messageStartY + Math.max(frame.start, frame.end - 1) * messageGap + 42;
    nodes.push({
      id: nodeId('fragment', index),
      type: 'composite_shape',
      x: frameX,
      y: startY,
      width: frameWidth,
      height: Math.max(84, endY - startY),
      text: {
        ...boardText(`${frame.kind}${frame.label ? ` ${frame.label}` : ''}`, 14, 'bold'),
        horizontal_align: 'left',
        vertical_align: 'top',
      },
      style: {
        fill_color: '#FFFFFF',
        fill_opacity: 0.05,
        border_color: '#64748B',
        border_style: 'dash',
        border_width: 'narrow',
      },
      composite_shape: { type: 'rect' },
      z_index: 0,
    });
    frame.separators.forEach((separator) => {
      controlSeparators.push({ frameIndex: index, ...separator });
    });
  });
  controlSeparators.forEach((separator, index) => {
    const y = messageStartY + separator.index * messageGap - 36;
    nodes.push({
      id: nodeId('separator', index),
      type: 'connector',
      style: {
        border_color: '#94A3B8',
        border_style: 'dash',
        border_width: 'extra_narrow',
      },
      connector: {
        start: { position: { x: frameX, y }, arrow_style: 'none' },
        end: { position: { x: frameX + frameWidth, y }, arrow_style: 'none' },
        captions: separator.label
          ? { data: [boardText(`else ${separator.label}`, 13, 'regular')] }
          : undefined,
        shape: 'straight',
        caption_auto_direction: true,
        specified_coordinate: true,
      },
      z_index: 1,
    });
  });

  const width = marginX * 2 + actors.length * actorWidth + (actors.length - 1) * actorGap;
  return {
    schemaVersion: 1,
    diagramType: 'sequence',
    width: Math.max(800, width),
    height: Math.max(480, lifelineEndY + bottomPadding),
    nodes,
    semantics: { noteCount, autoNumber },
  };
}

function convertStateDiagram(db) {
  const states = Array.from(db.getStates().values());
  const relations = db.getRelations();
  if (states.length === 0) throw new Error('State diagram contains no states.');
  const vertices = states.map((state) => ({ id: state.id, source: state }));
  const edges = relations.map((relation) => ({
    start: relation.id1,
    end: relation.id2,
    text: relation.relationTitle,
  }));
  return convertLayeredGraph({
    vertices,
    edges,
    diagramType: 'state',
    nodeWidth: 210,
    nodeHeight: 76,
    nodeFactory: (vertex, index, position) => {
      const pseudo = isPseudoState(vertex.id);
      return {
        id: nodeId('state', index),
        mermaidId: vertex.id,
        type: 'composite_shape',
        x: pseudo ? position.x + 77 : position.x,
        y: pseudo ? position.y + 10 : position.y,
        width: pseudo ? 56 : 210,
        height: pseudo ? 56 : 76,
        text: pseudo ? undefined : boardText(cleanText(vertex.source.description || vertex.id), 15, 'bold'),
        style: {
          fill_color: pseudo ? '#334155' : '#F0FDF4',
          fill_opacity: 1,
          border_color: pseudo ? '#0F172A' : '#15803D',
          border_style: 'solid',
          border_width: pseudo ? 'bold' : 'narrow',
        },
        composite_shape: { type: pseudo ? 'ellipse' : 'round_rect2' },
        z_index: 2,
      };
    },
    connectorStyle: () => ({
      border_color: '#475569',
      border_style: 'solid',
      border_width: 'narrow',
    }),
    endArrow: () => 'triangle_arrow',
  });
}

function convertClassDiagram(db) {
  const classes = Array.from(db.getClasses().values());
  const relations = db.getRelations();
  if (classes.length === 0) throw new Error('Class diagram contains no classes.');
  const vertices = classes.map((classInfo) => ({ id: classInfo.id, source: classInfo }));
  const edges = relations.map((relation) => ({
    start: relation.id1,
    end: relation.id2,
    text: classRelationCaption(relation),
    source: relation,
  }));
  return convertLayeredGraph({
    vertices,
    edges,
    diagramType: 'class',
    nodeWidth: 260,
    nodeHeight: 150,
    nodeFactory: (vertex, index, position) => {
      const classInfo = vertex.source;
      const attributes = (classInfo.members ?? []).map((item) => cleanText(item.text));
      const methods = (classInfo.methods ?? []).map((item) => cleanText(item.text));
      const sections = [cleanText(classInfo.label || vertex.id)];
      if (attributes.length) sections.push(attributes.join('\n'));
      if (methods.length) sections.push(methods.join('\n'));
      return {
        id: nodeId('class', index),
        mermaidId: vertex.id,
        type: 'composite_shape',
        x: position.x,
        y: position.y,
        width: 260,
        height: 150,
        text: boardText(sections.join('\n────────\n'), 14, 'regular'),
        style: {
          fill_color: '#F8FAFC',
          fill_opacity: 1,
          border_color: '#334155',
          border_style: 'solid',
          border_width: 'narrow',
        },
        composite_shape: { type: 'rect' },
        z_index: 2,
      };
    },
    connectorStyle: (edge) => ({
      border_color: '#475569',
      border_style: edge.source.relation.lineType === 1 ? 'dash' : 'solid',
      border_width: 'narrow',
    }),
    startArrow: (edge) => classRelationArrow(edge.source.relation.type1),
    endArrow: (edge) => classRelationArrow(edge.source.relation.type2),
  });
}

function convertMindmap(db) {
  const root = db.getMindmap();
  if (!root) throw new Error('Mind map contains no root node.');
  const entries = [];
  const walk = (item, parentId = null) => {
    const id = String(item.nodeId ?? item.id);
    entries.push({ id, parentId, depth: item.level ?? 0, source: item });
    for (const child of item.children ?? []) walk(child, id);
  };
  walk(root);
  const byDepth = new Map();
  for (const entry of entries) {
    if (!byDepth.has(entry.depth)) byDepth.set(entry.depth, []);
    byDepth.get(entry.depth).push(entry);
  }
  const positions = new Map();
  for (const [depth, items] of byDepth) {
    items.forEach((item, lane) => positions.set(item.id, {
      x: 80 + depth * 310,
      y: 70 + lane * 130,
    }));
  }
  const shapeNodes = entries.map((entry, index) => {
    const position = positions.get(entry.id);
    return {
      id: nodeId('mind', index),
      mermaidId: entry.id,
      type: 'composite_shape',
      x: position.x,
      y: position.y,
      width: 220,
      height: 76,
      text: boardText(cleanText(entry.source.descr || entry.id), entry.depth === 0 ? 17 : 15, entry.depth <= 1 ? 'bold' : 'regular'),
      style: {
        fill_color: entry.depth === 0 ? '#DBEAFE' : entry.depth === 1 ? '#ECFDF5' : '#F8FAFC',
        fill_opacity: 1,
        border_color: entry.depth === 0 ? '#2563EB' : entry.depth === 1 ? '#059669' : '#64748B',
        border_style: 'solid',
        border_width: 'narrow',
      },
      composite_shape: { type: entry.depth === 0 ? 'ellipse' : 'round_rect2' },
      z_index: 2,
    };
  });
  const boardIdByMermaidId = new Map(shapeNodes.map((node) => [node.mermaidId, node.id]));
  shapeNodes.forEach((node) => delete node.mermaidId);
  const connectors = entries.filter((entry) => entry.parentId !== null).map((entry, index) => ({
    id: nodeId('mind_edge', index),
    type: 'connector',
    style: { border_color: '#64748B', border_style: 'solid', border_width: 'narrow' },
    connector: {
      start: { attached_object: { id: boardIdByMermaidId.get(entry.parentId), snap_to: 'right' }, arrow_style: 'none' },
      end: { attached_object: { id: boardIdByMermaidId.get(entry.id), snap_to: 'left' }, arrow_style: 'none' },
      shape: 'curve',
    },
    z_index: 1,
  }));
  const maxLane = Math.max(...Array.from(byDepth.values(), (items) => items.length));
  const maxDepth = Math.max(...entries.map((entry) => entry.depth));
  return {
    schemaVersion: 1,
    diagramType: 'mindmap',
    width: Math.max(800, 80 + (maxDepth + 1) * 310),
    height: Math.max(480, 80 + maxLane * 130),
    nodes: [...shapeNodes, ...connectors],
    semantics: { mindmapNodeCount: entries.length },
  };
}

function convertLayeredGraph(options) {
  const rankById = calculateRanks(options.vertices, options.edges);
  const ranks = new Map();
  for (const vertex of options.vertices) {
    const rank = rankById.get(vertex.id) ?? 0;
    if (!ranks.has(rank)) ranks.set(rank, []);
    ranks.get(rank).push(vertex);
  }
  const rankKeys = Array.from(ranks.keys()).sort((a, b) => a - b);
  const maxLaneCount = Math.max(...Array.from(ranks.values(), (items) => items.length));
  const positions = new Map();
  for (const rank of rankKeys) {
    const items = ranks.get(rank).sort((left, right) => left.id.localeCompare(right.id));
    const laneOffset = ((maxLaneCount - items.length) * (options.nodeWidth + 80)) / 2;
    items.forEach((item, lane) => positions.set(item.id, {
      x: 80 + laneOffset + lane * (options.nodeWidth + 80),
      y: 70 + rank * (options.nodeHeight + 115),
    }));
  }
  const shapeNodes = options.vertices.map((vertex, index) => options.nodeFactory(vertex, index, positions.get(vertex.id)));
  const boardIdByMermaidId = new Map(shapeNodes.map((node) => [node.mermaidId, node.id]));
  shapeNodes.forEach((node) => delete node.mermaidId);
  const connectors = options.edges.map((edge, index) => {
    const from = boardIdByMermaidId.get(edge.start);
    const to = boardIdByMermaidId.get(edge.end);
    if (!from || !to) throw new Error(`${options.diagramType} relation references an unknown node: ${edge.start} -> ${edge.end}`);
    return {
      id: nodeId(`${options.diagramType}_edge`, index),
      type: 'connector',
      style: options.connectorStyle(edge),
      connector: {
        start: { attached_object: { id: from, snap_to: 'bottom' }, arrow_style: options.startArrow?.(edge) ?? 'none' },
        end: { attached_object: { id: to, snap_to: 'top' }, arrow_style: options.endArrow?.(edge) ?? 'triangle_arrow' },
        captions: edge.text ? { data: [boardText(cleanText(edge.text), 13, 'regular')] } : undefined,
        shape: 'right_angled_polyline',
        caption_auto_direction: true,
      },
      z_index: 1,
    };
  });
  const maxX = Math.max(...Array.from(positions.values(), (item) => item.x + options.nodeWidth));
  const maxY = Math.max(...Array.from(positions.values(), (item) => item.y + options.nodeHeight));
  return {
    schemaVersion: 1,
    diagramType: options.diagramType,
    width: Math.max(800, maxX + 80),
    height: Math.max(480, maxY + 80),
    nodes: [...shapeNodes, ...connectors],
    semantics: {
      nodeCount: options.vertices.length,
      edgeCount: options.edges.length,
    },
  };
}

function isPseudoState(id) {
  return /(?:^|_)(?:start|end)$/i.test(String(id));
}

function classRelationArrow(type) {
  return type === 0 ? 'none' : type === 1 ? 'triangle_arrow' : 'line_arrow';
}

function classRelationCaption(relation) {
  const labels = new Map([
    [1, 'inherits'],
    [2, 'composition'],
    [3, 'aggregation'],
    [4, 'dependency'],
  ]);
  const relationLabel = labels.get(relation.relation.type1)
    ?? labels.get(relation.relation.type2)
    ?? '';
  return [relation.title, relationLabel].filter(Boolean).join(' · ');
}

function calculateRanks(vertices, edges) {
  const ids = new Set(vertices.map((vertex) => vertex.id));
  const indegree = new Map(vertices.map((vertex) => [vertex.id, 0]));
  const outgoing = new Map(vertices.map((vertex) => [vertex.id, []]));
  for (const edge of edges) {
    if (!ids.has(edge.start) || !ids.has(edge.end) || edge.start === edge.end) continue;
    outgoing.get(edge.start).push(edge.end);
    indegree.set(edge.end, indegree.get(edge.end) + 1);
  }

  const rank = new Map(vertices.map((vertex) => [vertex.id, 0]));
  const queue = Array.from(ids).filter((id) => indegree.get(id) === 0).sort();
  const visited = new Set();
  while (queue.length > 0) {
    const id = queue.shift();
    visited.add(id);
    for (const next of outgoing.get(id)) {
      rank.set(next, Math.max(rank.get(next), rank.get(id) + 1));
      indegree.set(next, indegree.get(next) - 1);
      if (indegree.get(next) === 0) queue.push(next);
    }
    queue.sort();
  }

  for (const id of Array.from(ids).sort()) {
    if (visited.has(id)) continue;
    const predecessorRanks = edges
      .filter((edge) => edge.end === id && rank.has(edge.start))
      .map((edge) => rank.get(edge.start));
    rank.set(id, predecessorRanks.length ? Math.max(...predecessorRanks) + 1 : 0);
    visited.add(id);
  }
  return rank;
}

function boardText(text, fontSize, fontWeight) {
  return {
    text,
    font_size: fontSize,
    font_weight: fontWeight,
    horizontal_align: 'center',
    vertical_align: 'mid',
    text_color: '#0F172A',
  };
}

function boardShape(type) {
  const mapping = {
    circle: 'ellipse',
    cylinder: 'flow_chart_cylinder',
    diamond: 'flow_chart_diamond',
    doublecircle: 'ellipse',
    hexagon: 'flow_chart_hexagon',
    lean_left: 'flow_chart_parallelogram',
    lean_right: 'flow_chart_parallelogram',
    odd: 'rect',
    stadium: 'flow_chart_round_rect',
    subroutine: 'predefined_process',
    trapezoid: 'flow_chart_trapezoid',
  };
  return mapping[type] ?? 'round_rect2';
}

function shapeStyle(type) {
  const decision = type === 'diamond' || type === 'hexagon';
  return {
    fill_color: decision ? '#FFF7ED' : '#F8FAFC',
    fill_opacity: 1,
    border_color: decision ? '#EA580C' : '#334155',
    border_style: 'solid',
    border_width: 'narrow',
  };
}

function cleanText(value) {
  const text = String(value)
    .replace(/<br\s*\/?>/gi, '\n')
    .replace(/<[^>]+>/g, '')
    .replace(/</g, '<')
    .replace(/>/g, '>')
    .replace(/&/g, '&')
    .trim();
  if (text.length >= 2) {
    const first = text[0];
    const last = text[text.length - 1];
    if ((first === '"' && last === '"') || (first === "'" && last === "'")) {
      return text.slice(1, -1).trim();
    }
  }
  return text;
}

function nodeId(kind, index) {
  return `mermaid_${kind}_${String(index + 1).padStart(4, '0')}`;
}
