<script setup lang="ts">
import { onMounted } from 'vue';

import { updateWidgetSettings, widgetId, widgetName } from '@/stores/config';
import StatusIndicator from './components/StatusIndicator.vue';
import { useWidgetSocket } from './hooks/useWidgetSocket';

// Initialize WebSocket connection
const socket = useWidgetSocket(widgetId);

function handleSettingsUpdated(newSettings: any) {
  console.log('Received settings update:', newSettings);
  updateWidgetSettings(newSettings);
}

onMounted(async () => {
  await socket.connect();
  socket.on('SettingsUpdated', handleSettingsUpdated);
});

onMounted(() => {
  socket.off('SettingsUpdated', handleSettingsUpdated);
});
</script>

<template>
  <div class="widget-container">
    <!-- Widget Header -->
    <header class="flex items-center justify-between mb-6 p-4 bg-neutral-800/50 rounded-lg backdrop-blur-sm">
      <h1 class="text-2xl font-bold text-white">
        {{ widgetName }}
      </h1>
      <StatusIndicator :is-connected="socket.state.isConnected" />
    </header>

    <!-- Main Content Area -->
    <main class="flex-1 p-4">
      <!-- Your widget content goes here -->
      <div class="text-center text-neutral-300">
        <p class="text-lg mb-4">
          Widget is ready!
        </p>
        <p class="text-sm">
          Connected to widget hub: <span :class="socket.state.isConnected ? 'text-green-400' : 'text-red-400'">
						{{ socket.state.isConnected ? 'Yes' : 'No' }}
					</span>
        </p>
      </div>
    </main>

    <!-- Debug Info (remove in production) -->
    <div class="absolute top-2 right-2 text-xs text-neutral-500 bg-black/20 p-2 rounded">
      Widget ID: {{ widgetId }}<br>
    </div>
  </div>
</template>
