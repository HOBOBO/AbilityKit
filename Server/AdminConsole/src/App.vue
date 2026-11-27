<template>
  <div class="admin-layout shell">
    <AdminSidebar
      :items="adminNavigationItems"
      :active-key="routeKey"
      :server-mode-label="admin.serverModeLabel.value"
      :environment-name="admin.serverStatus.value?.environmentName"
      :maintenance-mode="admin.serverStatus.value?.maintenanceMode"
      @navigate="navigate" />

    <div class="workspace">
      <AdminTopbar :title="currentRoute.label" :description="currentRoute.description" :busy="admin.busy.value" @refresh="admin.refreshDashboard" />

      <AdminWorkspaceSummary :current-route-label="currentRoute.label" :current-route-description="currentRoute.description" />

      <AdminOverviewMetrics v-if="routeKey === 'overview'" />

      <main class="content-grid grid">
        <template v-if="routeKey === 'overview'">
          <AdminOperationsCenter />
          <AdminApiBoundaryPanel />
        </template>

        <AdminSessionPage :route-key="routeKey" />
        <AdminOpsPage :route-key="routeKey" />
        <AdminClusterPage :route-key="routeKey" />
        <AdminRoomsPage :route-key="routeKey" />
        <AdminBattlePage :route-key="routeKey" />

        <template v-if="routeKey === 'skills'">
          <SkillScenarioArchivePanel />
          <SkillDiagnosticsPanel />
        </template>

        <section v-if="routeKey === 'debug'" id="debug" class="card span-12 debug-panel">
          <div class="card-head"><div><p class="section-kicker">接口响应</p><h2>响应 / 聚合状态</h2></div><span class="badge">JSON</span></div>
          <pre>{{ admin.lastResponse.value || '等待操作...' }}</pre>
        </section>
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, watch } from 'vue';
import AdminSidebar from './components/layout/AdminSidebar.vue';
import AdminTopbar from './components/layout/AdminTopbar.vue';
import AdminWorkspaceSummary from './components/layout/AdminWorkspaceSummary.vue';
import AdminApiBoundaryPanel from './components/overview/AdminApiBoundaryPanel.vue';
import AdminOperationsCenter from './components/overview/AdminOperationsCenter.vue';
import AdminOverviewMetrics from './components/overview/AdminOverviewMetrics.vue';
import SkillDiagnosticsPanel from './components/skills/SkillDiagnosticsPanel.vue';
import SkillScenarioArchivePanel from './components/skills/SkillScenarioArchivePanel.vue';
import { adminNavigationItems } from './navigation/adminNavigation';
import AdminBattlePage from './pages/AdminBattlePage.vue';
import AdminClusterPage from './pages/AdminClusterPage.vue';
import AdminOpsPage from './pages/AdminOpsPage.vue';
import AdminRoomsPage from './pages/AdminRoomsPage.vue';
import AdminSessionPage from './pages/AdminSessionPage.vue';
import { useAdminRouter } from './router/adminRouter';
import { useAdminConsoleStore } from './stores/adminConsoleStore';

const admin = useAdminConsoleStore();
const { routeKey, currentRoute, navigate } = useAdminRouter();

onMounted(admin.refreshAdminWorkspace);
watch(routeKey, () => { void admin.refreshAdminWorkspace(); });
</script>
