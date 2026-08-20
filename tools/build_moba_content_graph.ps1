[CmdletBinding()]
param(
    [string]$GraphPath = 'artifacts\moba-content\moba-content-graph.json',
    [string]$DiagnosticsPath = 'artifacts\moba-content\moba-content-diagnostics.json',
    [string]$ReportPath = '',
    [string]$HtmlOutputPath = 'artifacts\moba-content\content-dependency-graph.html',
    [string]$DotOutputPath = 'artifacts\moba-content\content-dependency-graph.dot'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Resolve-RepoPath {
    param([string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) { return [System.IO.Path]::GetFullPath($Path) }
    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Ensure-ParentDirectory {
    param([string]$Path)
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        $null = New-Item -ItemType Directory -Force -Path $parent
    }
}

function Escape-Dot {
    param([string]$Value)
    return $Value.Replace('\', '\\').Replace('"', '\"')
}

$useLegacyReport = -not [string]::IsNullOrWhiteSpace($ReportPath)
$nodes = New-Object System.Collections.Generic.List[object]
$roots = New-Object System.Collections.Generic.List[object]
$linkGroups = @{}
$issueCounts = @{}

if ($useLegacyReport) {
    $resolvedReportPath = Resolve-RepoPath $ReportPath
    if (-not (Test-Path -LiteralPath $resolvedReportPath -PathType Leaf)) { throw "MOBA content dependency report was not found: $ReportPath" }
    $report = Get-Content -LiteralPath $resolvedReportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([int]$report.schemaVersion -ne 1 -or [string]$report.report -ne 'moba-content-dependency') {
        throw 'MOBA content graph requires a schemaVersion 1 moba-content-dependency report.'
    }
    foreach ($issue in @($report.issues)) {
        $tableName = if ($issue.PSObject.Properties['sourceTable']) { [string]$issue.sourceTable } elseif ($issue.PSObject.Properties['table']) { [string]$issue.table } else { '' }
        if ([string]::IsNullOrWhiteSpace($tableName)) { continue }
        if (-not $issueCounts.ContainsKey($tableName)) { $issueCounts[$tableName] = [ordered]@{ errors = 0; warnings = 0 } }
        if ([string]$issue.severity -eq 'error') { $issueCounts[$tableName].errors++ } else { $issueCounts[$tableName].warnings++ }
    }
    foreach ($table in @($report.tables | Sort-Object name)) {
        $counts = if ($issueCounts.ContainsKey([string]$table.name)) { $issueCounts[[string]$table.name] } else { [ordered]@{ errors = 0; warnings = 0 } }
        $nodes.Add([pscustomobject][ordered]@{ id = [string]$table.name; label = [string]$table.name; kind = 'table'; records = [int]$table.recordCount; unreferenced = @($table.unreferencedIds).Count; errors = [int]$counts.errors; warnings = [int]$counts.warnings })
    }
    foreach ($edge in @($report.edges)) {
        $key = "$($edge.sourceTable)|$($edge.targetTable)"
        if (-not $linkGroups.ContainsKey($key)) { $linkGroups[$key] = [ordered]@{ source = [string]$edge.sourceTable; target = [string]$edge.targetTable; resolved = 0; missing = 0; kind = 'static' } }
        if ([string]$edge.status -eq 'resolved') { $linkGroups[$key].resolved++ } else { $linkGroups[$key].missing++ }
    }
    foreach ($external in @($report.externalReferences | Where-Object { [int]$_.referenceCount -gt 0 })) {
        $authorityId = 'authority:' + [string]$external.authority
        if (-not @($nodes | Where-Object id -eq $authorityId).Count) { $nodes.Add([pscustomobject][ordered]@{ id = $authorityId; label = [string]$external.authority; kind = 'authority'; records = 0; unreferenced = 0; errors = 0; warnings = 0 }) }
        $key = "$($external.sourceTable)|$authorityId"
        if (-not $linkGroups.ContainsKey($key)) { $linkGroups[$key] = [ordered]@{ source = [string]$external.sourceTable; target = $authorityId; resolved = 0; missing = 0; kind = 'external' } }
        $linkGroups[$key].resolved += [int]$external.referenceCount
    }
    foreach ($root in @($report.roots | Sort-Object kind, id)) {
        $roots.Add([pscustomobject][ordered]@{ id = [long]$root.id; name = [string]$root.name; tables = @($root.reachableNodes | ForEach-Object { ([string]$_).Split(':')[0] } | Sort-Object -Unique); reachableNodes = [int]$root.reachableNodeCount; issues = [int]$root.reachableIssueCount })
    }
    $viewSummary = $report.summary
    $viewStatus = [string]$report.status
}
else {
    $resolvedGraphPath = Resolve-RepoPath $GraphPath
    if (-not (Test-Path -LiteralPath $resolvedGraphPath -PathType Leaf)) { throw "MOBA content graph IR was not found: $GraphPath" }
    $graph = Get-Content -LiteralPath $resolvedGraphPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([int]$graph.schemaVersion -ne 1 -or [string]$graph.graph -ne 'moba-content-graph') { throw 'MOBA content graph viewer requires a schemaVersion 1 moba-content-graph document.' }
    $graphNodeMap = @{}
    foreach ($node in @($graph.nodes)) { $graphNodeMap[[string]$node.id] = $node }

    $resolvedDiagnosticsPath = Resolve-RepoPath $DiagnosticsPath
    if (Test-Path -LiteralPath $resolvedDiagnosticsPath -PathType Leaf) {
        $diagnostics = Get-Content -LiteralPath $resolvedDiagnosticsPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ([int]$diagnostics.schemaVersion -ne 1 -or [string]$diagnostics.diagnostics -ne 'moba-content-diagnostics') { throw 'MOBA content graph viewer requires schemaVersion 1 diagnostics.' }
        foreach ($diagnostic in @($diagnostics.items)) {
            $tableName = ''
            if ($diagnostic.nodeId -and $graphNodeMap.ContainsKey([string]$diagnostic.nodeId) -and $graphNodeMap[[string]$diagnostic.nodeId].PSObject.Properties['table']) { $tableName = [string]$graphNodeMap[[string]$diagnostic.nodeId].table }
            if ([string]::IsNullOrWhiteSpace($tableName)) { continue }
            if (-not $issueCounts.ContainsKey($tableName)) { $issueCounts[$tableName] = [ordered]@{ errors = 0; warnings = 0 } }
            if ([string]$diagnostic.severity -eq 'error') { $issueCounts[$tableName].errors++ } else { $issueCounts[$tableName].warnings++ }
        }
    }
    foreach ($tableNode in @($graph.nodes | Where-Object kind -eq 'table' | Sort-Object table)) {
        $tableName = [string]$tableNode.table
        $counts = if ($issueCounts.ContainsKey($tableName)) { $issueCounts[$tableName] } else { [ordered]@{ errors = 0; warnings = 0 } }
        $unreferenced = @($graph.nodes | Where-Object { $_.kind -eq 'record' -and $_.table -eq $tableName -and [bool]$_.unreferenced }).Count
        $nodes.Add([pscustomobject][ordered]@{ id = $tableName; label = $tableName; kind = 'table'; records = [int]$tableNode.recordCount; unreferenced = $unreferenced; errors = [int]$counts.errors; warnings = [int]$counts.warnings })
    }
    foreach ($authority in @($graph.nodes | Where-Object kind -eq 'authority' | Sort-Object id)) {
        $nodes.Add([pscustomobject][ordered]@{ id = [string]$authority.id; label = [string]$authority.authority; kind = 'authority'; records = 0; unreferenced = 0; errors = 0; warnings = 0 })
    }
    foreach ($edge in @($graph.edges)) {
        if ([string]$edge.kind -eq 'reference') {
            $sourceTable = [string]$graphNodeMap[[string]$edge.source].table
            $targetTable = [string]$graphNodeMap[[string]$edge.target].table
            $key = "$sourceTable|$targetTable"
            if (-not $linkGroups.ContainsKey($key)) { $linkGroups[$key] = [ordered]@{ source = $sourceTable; target = $targetTable; resolved = 0; missing = 0; kind = 'static' } }
            if ([string]$edge.status -eq 'resolved') { $linkGroups[$key].resolved++ } else { $linkGroups[$key].missing++ }
        }
        elseif ([string]$edge.kind -eq 'authority-reference') {
            $sourceTable = [string]$graphNodeMap[[string]$edge.source].table
            $key = "$sourceTable|$([string]$edge.target)"
            if (-not $linkGroups.ContainsKey($key)) { $linkGroups[$key] = [ordered]@{ source = $sourceTable; target = [string]$edge.target; resolved = 0; missing = 0; kind = 'external' } }
            $linkGroups[$key].resolved += [int]$edge.referenceCount
        }
    }
    foreach ($root in @($graph.roots | Sort-Object kind, id)) {
        $tables = @($root.reachableNodeIds | ForEach-Object { if ($graphNodeMap.ContainsKey([string]$_)) { [string]$graphNodeMap[[string]$_].table } } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
        $rootIssueCount = if (Get-Variable diagnostics -ErrorAction SilentlyContinue) { @($diagnostics.items | Where-Object { $_.rootIds -contains [string]$root.id }).Count } else { 0 }
        $rootId = ([string]$root.id).Split(':')[-1]
        $roots.Add([pscustomobject][ordered]@{ id = [long]$rootId; name = [string]$root.name; tables = $tables; reachableNodes = @($root.reachableNodeIds).Count; issues = $rootIssueCount })
    }
    $viewSummary = [ordered]@{ tables = [int]$graph.summary.tables; records = [int]$graph.summary.records; edges = ([int]$graph.summary.resolvedEdges + [int]$graph.summary.missingEdges); errors = $(if (Get-Variable diagnostics -ErrorAction SilentlyContinue) { [int]$diagnostics.summary.errors } else { 0 }); warnings = $(if (Get-Variable diagnostics -ErrorAction SilentlyContinue) { [int]$diagnostics.summary.warnings } else { 0 }) }
    $viewStatus = [string]$graph.status
}

$links = @($linkGroups.Values | Sort-Object source, target | ForEach-Object { [pscustomobject]$_ })
$graphData = [ordered]@{ status = $viewStatus; summary = $viewSummary; nodes = @($nodes.ToArray() | Sort-Object kind, id); links = $links; roots = @($roots.ToArray()) }
$graphJson = $graphData | ConvertTo-Json -Depth 16 -Compress
$graphBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($graphJson))

$dotLines = New-Object System.Collections.Generic.List[string]
$dotLines.Add('digraph MobaContentDependency {')
$dotLines.Add('  rankdir=LR;')
$dotLines.Add('  graph [fontname="Arial", bgcolor="transparent", nodesep=0.35, ranksep=0.8];')
$dotLines.Add('  node [shape=box, style="rounded,filled", fontname="Arial", color="#687078", fillcolor="#f5f6f7"];')
$dotLines.Add('  edge [fontname="Arial", color="#687078"];')
foreach ($node in @($graphData.nodes)) {
    $shape = if ($node.kind -eq 'authority') { 'component' } else { 'box' }
    $label = if ($node.kind -eq 'table') { "$($node.label)\n$($node.records) records" } else { $node.label }
    $dotLines.Add(('  "{0}" [label="{1}", shape={2}];' -f (Escape-Dot $node.id), (Escape-Dot $label), $shape))
}
foreach ($link in $links) {
    $style = if ($link.kind -eq 'external') { ', style=dashed' } else { '' }
    $label = if ($link.missing -gt 0) { "$($link.resolved) resolved / $($link.missing) missing" } else { [string]$link.resolved }
    $dotLines.Add(('  "{0}" -> "{1}" [label="{2}"{3}];' -f (Escape-Dot $link.source), (Escape-Dot $link.target), (Escape-Dot $label), $style))
}
$dotLines.Add('}')

$html = @'
<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>MOBA Content Dependency Graph</title>
<style>
:root{color-scheme:light;--bg:#f7f8f9;--surface:#fff;--text:#202428;--muted:#646b73;--border:#d9dde1;--line:#87909a;--accent:#176b87;--accent-soft:#dff1f5;--ok:#27744a;--warn:#a45c08;--warn-soft:#fff0d6;--bad:#b3261e;--bad-soft:#fde7e5;--authority:#5d467d;--authority-soft:#eee8f6}
@media(prefers-color-scheme:dark){:root{color-scheme:dark;--bg:#17191b;--surface:#202326;--text:#edf0f2;--muted:#aeb5bc;--border:#3d4349;--line:#89939d;--accent:#68bad3;--accent-soft:#183a45;--ok:#67bd8d;--warn:#e3a24b;--warn-soft:#432f16;--bad:#f28b82;--bad-soft:#4a211f;--authority:#bba3dc;--authority-soft:#352b43}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:14px/1.45 system-ui,-apple-system,"Segoe UI",sans-serif;letter-spacing:0}header{padding:18px 22px 12px;border-bottom:1px solid var(--border);background:var(--surface)}h1{font-size:20px;font-weight:500;margin:0 0 10px;letter-spacing:0}.summary{display:flex;gap:18px;flex-wrap:wrap;color:var(--muted)}.summary strong{color:var(--text);font-weight:500}.toolbar{display:flex;gap:12px;align-items:end;flex-wrap:wrap;padding:12px 22px;border-bottom:1px solid var(--border);background:var(--surface)}label{display:grid;gap:4px;color:var(--muted)}select,input[type=search]{min-height:34px;padding:6px 9px;border:1px solid var(--border);border-radius:4px;background:var(--surface);color:var(--text);font:inherit}.check{display:flex;align-items:center;gap:6px;min-height:34px;color:var(--text)}main{display:grid;grid-template-columns:minmax(0,1fr) 280px;min-height:560px}.graph-wrap{overflow:auto;padding:10px}.detail{border-left:1px solid var(--border);background:var(--surface);padding:16px}.detail h2{font-size:16px;font-weight:500;margin:0 0 12px;overflow-wrap:anywhere}.detail dl{display:grid;grid-template-columns:1fr auto;gap:8px;margin:0}.detail dt{color:var(--muted)}.detail dd{margin:0;font-variant-numeric:tabular-nums}.legend{display:flex;gap:14px;flex-wrap:wrap;margin:0 0 8px;color:var(--muted)}.swatch{display:inline-block;width:10px;height:10px;border-radius:2px;margin-right:5px;background:var(--accent-soft);border:1px solid var(--accent)}.swatch.authority{background:var(--authority-soft);border-color:var(--authority)}.swatch.missing{background:var(--bad-soft);border-color:var(--bad)}svg{display:block;min-width:900px;width:100%;height:auto}.edge{fill:none;stroke:var(--line);stroke-width:1.3;marker-end:url(#arrow)}.edge.external{stroke-dasharray:5 4}.edge.missing{stroke:var(--bad);stroke-width:2}.edge-label{fill:var(--muted);font-size:11px;paint-order:stroke;stroke:var(--bg);stroke-width:3px}.node rect{fill:var(--accent-soft);stroke:var(--accent);stroke-width:1.2;rx:5}.node.authority rect{fill:var(--authority-soft);stroke:var(--authority)}.node.warning rect{fill:var(--warn-soft);stroke:var(--warn)}.node.error rect{fill:var(--bad-soft);stroke:var(--bad)}.node.selected rect{stroke-width:3}.node text{fill:var(--text);pointer-events:none}.node .name{font-weight:500}.node .meta{fill:var(--muted);font-size:11px}.empty{padding:40px;color:var(--muted);text-align:center}@media(max-width:760px){header,.toolbar{padding-left:14px;padding-right:14px}main{grid-template-columns:1fr}.detail{border-left:0;border-top:1px solid var(--border)}select,input[type=search]{max-width:100%;width:100%}label{flex:1 1 180px}}
</style>
</head>
<body>
<header><h1>MOBA Content Dependency Graph</h1><div class="summary" id="summary"></div></header>
<div class="toolbar">
  <label>Hero<select id="hero"></select></label>
  <label>Table filter<input id="search" type="search" placeholder="characters / trigger_plans"></label>
  <label class="check"><input id="external" type="checkbox" checked>Show external authority</label>
</div>
<main>
  <section class="graph-wrap" aria-label="Content dependency graph"><div class="legend"><span><i class="swatch"></i>Config table</span><span><i class="swatch authority"></i>External authority</span><span><i class="swatch missing"></i>Missing references</span></div><svg id="graph" role="img" aria-label="MOBA config table dependencies"></svg><div id="empty" class="empty" hidden>No nodes match the current filter</div></section>
  <aside class="detail" aria-live="polite"><h2 id="detail-title">Select a node</h2><dl id="detail-body"></dl></aside>
</main>
<script>
const data=JSON.parse(new TextDecoder().decode(Uint8Array.from(atob('__DATA_BASE64__'),c=>c.charCodeAt(0))));
const byId=new Map(data.nodes.map(n=>[n.id,n]));
const svg=document.getElementById('graph'),hero=document.getElementById('hero'),search=document.getElementById('search'),external=document.getElementById('external'),empty=document.getElementById('empty');
let selected='';
document.getElementById('summary').innerHTML=`<span><strong>${data.summary.tables}</strong> tables</span><span><strong>${data.summary.records}</strong> records</span><span><strong>${data.summary.edges}</strong> edges</span><span><strong>${data.summary.errors}</strong> errors</span><span><strong>${data.summary.warnings}</strong> warnings</span>`;
hero.innerHTML='<option value="">All heroes</option>'+data.roots.map(r=>`<option value="${r.id}">${r.name}</option>`).join('');
function esc(v){return String(v).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));}
function visibleData(){const root=data.roots.find(r=>String(r.id)===hero.value),q=search.value.trim().toLowerCase();let nodes=data.nodes.filter(n=>(external.checked||n.kind!=='authority')&&(!root||n.kind==='authority'||root.tables.includes(n.id))&&(!q||n.label.toLowerCase().includes(q)));const ids=new Set(nodes.map(n=>n.id));return{nodes,links:data.links.filter(l=>ids.has(l.source)&&ids.has(l.target))};}
function layout(nodes,links){const layers=new Map(),out=new Map();for(const n of nodes)out.set(n.id,[]);for(const l of links)if(out.has(l.source)&&out.has(l.target))out.get(l.source).push(l.target);if(out.has('characters'))layers.set('characters',0);let changed=true;for(let pass=0;pass<nodes.length&&changed;pass++){changed=false;for(const [source,targets] of out){if(!layers.has(source))continue;for(const target of targets){const next=layers.get(source)+1;if(!layers.has(target)||next<layers.get(target)){layers.set(target,next);changed=true;}}}}const known=[...layers.values()];const fallback=(known.length?Math.max(...known):0)+1;for(const n of nodes)if(!layers.has(n.id))layers.set(n.id,n.kind==='authority'?fallback+1:fallback);const groups=new Map();for(const n of nodes){const layer=layers.get(n.id);if(!groups.has(layer))groups.set(layer,[]);groups.get(layer).push(n)}for(const g of groups.values())g.sort((a,b)=>a.label.localeCompare(b.label));const pos=new Map();let maxRows=1,maxLayer=0;for(const [layer,g] of groups){maxRows=Math.max(maxRows,g.length);maxLayer=Math.max(maxLayer,layer);g.forEach((n,i)=>pos.set(n.id,{x:36+layer*270,y:36+i*78}))}return{pos,width:Math.max(1050,100+(maxLayer+1)*270),height:Math.max(420,72+maxRows*78)};}
function showDetail(id,links){const n=byId.get(id);if(!n)return;selected=id;const incoming=links.filter(l=>l.target===id),outgoing=links.filter(l=>l.source===id),missing=[...incoming,...outgoing].reduce((s,l)=>s+l.missing,0);document.getElementById('detail-title').textContent=n.label;document.getElementById('detail-body').innerHTML=`<dt>Type</dt><dd>${n.kind==='authority'?'authority':'config table'}</dd><dt>Records</dt><dd>${n.records}</dd><dt>Incoming types</dt><dd>${incoming.length}</dd><dt>Outgoing types</dt><dd>${outgoing.length}</dd><dt>Missing refs</dt><dd>${missing}</dd><dt>Unreferenced</dt><dd>${n.unreferenced}</dd><dt>Errors</dt><dd>${n.errors}</dd><dt>Warnings</dt><dd>${n.warnings}</dd>`;render();}
function render(){const v=visibleData();empty.hidden=v.nodes.length>0;svg.hidden=v.nodes.length===0;if(!v.nodes.length)return;const l=layout(v.nodes,v.links);svg.setAttribute('viewBox',`0 0 ${l.width} ${l.height}`);const defs='<defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse"><path d="M 0 0 L 10 5 L 0 10 z" fill="context-stroke"/></marker></defs>';const edges=v.links.map(e=>{const a=l.pos.get(e.source),b=l.pos.get(e.target),x1=a.x+210,y1=a.y+25,x2=b.x,y2=b.y+25,mx=(x1+x2)/2,path=`M${x1},${y1} C${mx},${y1} ${mx},${y2} ${x2},${y2}`,klass=`edge ${e.kind==='external'?'external':''} ${e.missing?'missing':''}`;return`<path class="${klass}" d="${path}"><title>${esc(e.source)} -> ${esc(e.target)}: ${e.resolved} resolved, ${e.missing} missing</title></path><text class="edge-label" x="${mx}" y="${(y1+y2)/2-4}" text-anchor="middle">${e.resolved}${e.missing?' / '+e.missing+' missing':''}</text>`}).join('');const nodeHtml=v.nodes.map(n=>{const p=l.pos.get(n.id),klass=`node ${n.kind} ${n.errors?'error':n.warnings?'warning':''} ${selected===n.id?'selected':''}`;return`<g class="${klass}" data-id="${esc(n.id)}" transform="translate(${p.x},${p.y})" role="button" tabindex="0" aria-label="${esc(n.label)}"><rect width="210" height="50"></rect><text class="name" x="10" y="20">${esc(n.label)}</text><text class="meta" x="10" y="38">${n.kind==='table'?n.records+' records':n.kind}${n.errors?' - '+n.errors+' errors':n.warnings?' - '+n.warnings+' warnings':''}</text></g>`}).join('');svg.innerHTML=defs+edges+nodeHtml;svg.querySelectorAll('.node').forEach(el=>{const activate=()=>showDetail(el.dataset.id,v.links);el.addEventListener('click',activate);el.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();activate()}})});}
[hero,search,external].forEach(el=>el.addEventListener(el===search?'input':'change',()=>{selected='';render()}));render();
</script>
</body>
</html>
'@
$html = $html.Replace('__DATA_BASE64__', $graphBase64)

$resolvedHtmlOutputPath = Resolve-RepoPath $HtmlOutputPath
$resolvedDotOutputPath = Resolve-RepoPath $DotOutputPath
Ensure-ParentDirectory $resolvedHtmlOutputPath
Ensure-ParentDirectory $resolvedDotOutputPath
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedHtmlOutputPath, $html + [Environment]::NewLine, $utf8)
[System.IO.File]::WriteAllLines($resolvedDotOutputPath, $dotLines.ToArray(), $utf8)

Write-Host "MOBA content graph: nodes=$($graphData.nodes.Count) links=$($links.Count) html=$HtmlOutputPath dot=$DotOutputPath"
exit 0
