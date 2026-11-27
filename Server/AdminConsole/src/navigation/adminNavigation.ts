export type AdminRouteKey = 'overview' | 'session' | 'ops' | 'cluster' | 'rooms' | 'battle' | 'skills' | 'debug';

export interface AdminNavigationItem {
  key: AdminRouteKey;
  label: string;
  path: string;
  group: 'dashboard' | 'runtime' | 'diagnostics' | 'operations';
  description: string;
  requiresSession?: boolean;
  danger?: boolean;
}

export const adminNavigationItems: AdminNavigationItem[] = [
  { key: 'overview', label: '总览', path: '/overview', group: 'dashboard', description: '后台聚合概览' },
  { key: 'session', label: '会话', path: '/session', group: 'runtime', description: '账号与会话管理' },
  { key: 'rooms', label: '房间', path: '/rooms', group: 'runtime', description: '房间创建、当前房间与目录', requiresSession: true },
  { key: 'skills', label: '技能分析', path: '/skills', group: 'diagnostics', description: 'MOBA 批次排查、战斗数据与实时技能诊断' },
  { key: 'battle', label: '战斗', path: '/battle', group: 'runtime', description: '战斗启动与同步模板' },
  { key: 'cluster', label: '集群诊断', path: '/cluster', group: 'diagnostics', description: 'Orleans 与网关集群状态' },
  { key: 'ops', label: '运维', path: '/ops', group: 'operations', description: '维护、排空与重启请求', danger: true },
  { key: 'debug', label: '响应', path: '/debug', group: 'diagnostics', description: '最近接口响应数据' }
];

export function resolveAdminRouteKey(path: string): AdminRouteKey {
  const normalized = path.startsWith('/') ? path : `/${path}`;
  return adminNavigationItems.find(item => item.path === normalized)?.key || 'overview';
}
