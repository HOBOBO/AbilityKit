<template>
  <div class="shooter-component-detail entitas-detail">
    <section class="inspector-column entity-overview-column">
      <div class="trace-section-head"><h3>Entity</h3><span class="badge">{{ entity?.entityKind || 'None' }}</span></div>
      <div v-if="entity" class="selected-entity-hero compact">
        <div>
          <span>{{ entity.key }}</span>
          <strong>{{ entity.label }}</strong>
          <small>{{ entity.group }} / {{ entity.alive ? 'alive' : 'inactive' }}</small>
        </div>
        <div class="entity-pulse" :class="entity.entityKind.toLowerCase()">{{ entity.entityId }}</div>
      </div>
      <div v-if="entity" class="selected-entity-stats stacked">
        <div><span>Components</span><strong>{{ entity.components.length }}</strong></div>
        <div><span>Fields</span><strong>{{ fieldCount }}</strong></div>
        <div><span>Status</span><strong>{{ entity.alive ? 'Alive' : 'Inactive' }}</strong></div>
      </div>
      <p v-if="!entity" class="muted">请选择一个世界对象。</p>
    </section>

    <section class="inspector-column component-list-column">
      <div class="trace-section-head"><h3>Components</h3><span class="badge">{{ entity?.components.length || 0 }}</span></div>
      <button
        v-for="component in entity?.components || []"
        :key="`${entity?.key}-${component.name}`"
        type="button"
        class="component-list-row"
        :class="{ active: selectedComponent?.name === component.name }"
        @click="selectedComponentName = component.name">
        <strong>{{ component.componentKind }}</strong>
        <small>{{ component.name }}</small>
        <span>{{ fieldEntries(component.fields).length }} fields</span>
      </button>
      <p v-if="entity && entity.components.length === 0" class="muted">该实体没有导出的组件。</p>
    </section>

    <section class="inspector-column component-fields-column">
      <div class="trace-section-head"><h3>Component Data</h3><span class="badge">Fields</span></div>
      <div v-if="selectedComponent" class="component-data-head">
        <strong>{{ selectedComponent.name }}</strong>
        <span>{{ selectedComponent.componentKind }}</span>
      </div>
      <div v-if="selectedComponent" class="component-field-table inspector-field-table">
        <div v-for="entry in fieldEntries(selectedComponent.fields)" :key="entry.key" class="component-field-row">
          <span>{{ entry.key }}</span>
          <code>{{ entry.value }}</code>
        </div>
        <p v-if="fieldEntries(selectedComponent.fields).length === 0" class="muted">该组件没有导出的字段。</p>
      </div>
      <p v-if="entity && !selectedComponent" class="muted">请选择一个组件。</p>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { fieldEntries } from '../../composables/useShooterWorldProjection';
import type { ShooterWorldComponentDiagnostics, ShooterWorldEntityDiagnostics } from '../../types';

const props = defineProps<{
  entity: ShooterWorldEntityDiagnostics | null;
}>();

const selectedComponentName = ref('');
const fieldCount = computed(() => props.entity?.components.reduce((total, component) => total + fieldEntries(component.fields).length, 0) || 0);
const selectedComponent = computed<ShooterWorldComponentDiagnostics | null>(() => {
  const components = props.entity?.components || [];
  return components.find(component => component.name === selectedComponentName.value) || components[0] || null;
});

watch(() => props.entity?.key, () => {
  selectedComponentName.value = props.entity?.components[0]?.name || '';
}, { immediate: true });
</script>
