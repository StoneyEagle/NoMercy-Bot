<script lang="ts" setup>
import {ref, onMounted, watch} from 'vue';

import {BotTokenResponse, DeviceCode, User} from "@/types/auth.ts";

import serverClient from "@/lib/clients/serverClient";

import MoooomIcon from "@/components/icons/MoooomIcon.vue";
import {useTranslation} from "i18next-vue";

const {t} = useTranslation();

// State for the bot account
const botAccount = ref<User | null>();
const isLoading = ref(true);
const error = ref<string | null>(null);

// Device code flow state
const showDeviceCodeModal = ref(false);
const deviceCodeData = ref<DeviceCode | null>(null);
const isPolling = ref(false);
const pollingError = ref<string | null>(null);
const authSuccess = ref(false);
const authUsername = ref<string | null>(null);

// Fetch the bot account
const fetchBotAccount = async () => {
  try {
    isLoading.value = true;
    error.value = null;

    const response = await serverClient().get<User>('/bot-account');
    botAccount.value = response.data;
  } catch (err: any) {
    console.error('Failed to fetch bot account:', err);
    // If we get a 404, it just means no bot account exists yet - this is not an error
    if (err.response?.status === 404) {
      botAccount.value = undefined; // Ensure it's undefined for v-if="!botAccount" to work
    } else {
      error.value = 'settings.botAccounts.failedFetch';
    }
  } finally {
    isLoading.value = false;
  }
};

// Load the bot account on component mount
onMounted(fetchBotAccount);

const initiateDeviceCodeFlow = async () => {
  try {
    // Reset state
    authSuccess.value = false;
    authUsername.value = null;
    pollingError.value = null;

    // Get device code from API
    const response = await serverClient()
        .get<DeviceCode>('/bot/authenticate');
    deviceCodeData.value = response.data;
    showDeviceCodeModal.value = true;

    // Start polling for token
    await startPolling();
  } catch (err) {
    console.error('Failed to initiate device code flow:', err);
    error.value = 'Failed to initiate authentication';
  }
};

const startPolling = async () => {
  if (!deviceCodeData.value) return;

  isPolling.value = true;
  const {device_code, interval} = deviceCodeData.value;

  // Keep trying until we get a token or an error
  const pollInterval = setInterval(async () => {
    try {
      const response = await serverClient()
          .post<BotTokenResponse>('/bot/device/token', {
            deviceCode: device_code
          });

      // Success! We got a token
      authSuccess.value = true;
      authUsername.value = response.data?.username;
      clearInterval(pollInterval);
      isPolling.value = false;

      // Refresh the bot account
      setTimeout(() => {
        showDeviceCodeModal.value = false;
        fetchBotAccount();
      }, 2000);
    } catch (err: any) {
      console.log('Polling error:', err?.response?.data);

      // Check for authorization_pending in various possible locations in the error response
      const errorMessage = err?.response?.data?.message ||
          err?.response?.data?.detail ||
          err?.response?.data ||
          err?.message ||
          '';

      const isAuthorizationPending = typeof errorMessage === 'string' &&
          errorMessage.includes('authorization_pending');

      // If we get "authorization pending" error, keep polling
      if (isAuthorizationPending) {
        console.log('Authorization pending, continuing to poll...');
        return;
      }

      // Any other error, stop polling
      pollingError.value = typeof errorMessage === 'string' ?
          errorMessage :
          'Authentication failed';
      clearInterval(pollInterval);
      isPolling.value = false;
    }
  }, (interval || 5) * 1000); // Poll at the interval Twitch specified
};

const switchToClientCredentials = async () => {
  try {
    error.value = null;
    const response = await serverClient().post('/bot/client-credentials');
    if (response.data?.success) {
      await fetchBotAccount();
    }
  } catch (err: any) {
    console.error('Failed to switch to client credentials:', err);
    error.value = err?.response?.data?.message || 'Failed to switch to client credentials';
  }
};

const disconnectBot = async () => {
  // Using window.confirm with the translation string directly
  if (!confirm(t('settings.botAccounts.confirmDisconnect'))) return;

  try {
    await serverClient().delete('/bot-account');
    botAccount.value = null;
  } catch (err) {
    console.error('Failed to disconnect bot account:', err);
    error.value = 'settings.botAccounts.failedDisconnect';
  }
};

const closeModal = () => {
  showDeviceCodeModal.value = false;
};

// Clipboard copy function
const copyToClipboard = (text: string) => {
  if (navigator.clipboard && window.isSecureContext) {
    // Use the Clipboard API if available
    navigator.clipboard.writeText(text).then(() => {
      console.log('Copied to clipboard:', text);
    }).catch(err => {
      console.error('Failed to copy to clipboard:', err);
      fallbackCopyToClipboard(text); // Fallback in case of error
    });
  } else {
    // Fallback for browsers that don't support the Clipboard API
    fallbackCopyToClipboard(text);
  }
};

// Fallback function to copy text to clipboard
const fallbackCopyToClipboard = (text: string) => {
  // Create a temporary input element
  const input = document.createElement('input');
  input.value = text;
  document.body.appendChild(input);

  // Select and copy the text
  input.select();
  document.execCommand('copy');

  // Clean up
  document.body.removeChild(input);
  console.log('Copied to clipboard (fallback):', text);
};
</script>

<template>
  <div class="max-w-7xl w-full mx-auto mt-2 mb-4">
    <div v-if="isLoading" class="text-center text-gray-500 py-12">
      {{ $t('common.loading') }}
    </div>
    <div v-else-if="error" class="text-red-500 text-center py-12">
      {{ $t(error) }}
    </div>
    <div v-else>
      <div class="mb-8 bg-neutral-800/50 rounded-lg p-6 shadow-lg">
        <h2 class="text-xl font-bold mb-2 text-white">{{ $t('settings.botAccounts.title') }}</h2>
        <p class="text-neutral-300 mb-4">{{ $t('settings.botAccounts.description') }}</p>

        <div v-if="!botAccount" class="bg-neutral-700/30 rounded-md p-4 text-center">
          <p class="text-neutral-300 mb-4">{{ $t('settings.botAccounts.noBots') }}</p>
          <button @click="initiateDeviceCodeFlow"
                  class="px-4 py-2 bg-theme-600 hover:bg-theme-700 text-white rounded-md transition-colors">
            {{ $t('settings.botAccounts.connect') }}
          </button>
        </div>

        <div v-else class="bg-neutral-700/30 rounded-md p-6">
          <div class="flex flex-col space-y-6">
            <div class="flex items-center justify-between">
              <div class="flex items-center space-x-4">
                <div class="bg-theme-600/30 p-4 rounded-full">
                  <MoooomIcon icon="chatBot" class="h-8 w-8 text-theme-500"/>
                </div>
                <div>
                  <h3 class="text-xl font-bold text-white">
                    {{ botAccount.display_name || botAccount.username }}
                  </h3>
                  <div class="flex items-center mt-1">
                    <span
                        class="inline-flex items-center px-2 py-1 text-xs font-medium rounded-full bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300">
                      <span class="w-2 h-2 mr-1 bg-green-500 rounded-full"></span>
                      {{ $t('settings.botAccounts.connected') }}
                    </span>
                  </div>
                </div>
              </div>

              <div class="flex items-center gap-3">
                <button @click="switchToClientCredentials"
                        class="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-md transition-colors"
                        title="Switch to client credentials for bot badge">
                  Bot Badge Token
                </button>

                <button @click="initiateDeviceCodeFlow"
                        class="px-4 py-2 bg-theme-600 hover:bg-theme-700 text-white rounded-md transition-colors">
                  {{ $t('settings.botAccounts.change') }}
                </button>

                <button @click="disconnectBot"
                        class="p-2 text-neutral-400 hover:text-red-500 hover:bg-red-500/10 rounded-md transition-colors">
                  <MoooomIcon icon="trash" class="h-5 w-5"/>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Device Code Modal -->
    <div v-if="showDeviceCodeModal" class="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div class="bg-neutral-800 rounded-lg p-6 w-full max-w-md mx-4 shadow-xl">
        <div v-if="authSuccess" class="text-center">
          <div class="bg-green-500/20 p-4 rounded-full w-16 h-16 mx-auto mb-4 flex items-center justify-center">
            <MoooomIcon icon="checkDouble" class="h-5 w-5"/>
          </div>
          <h3 class="text-xl font-bold text-white mb-2">{{ $t('settings.botAccounts.authSuccess.title') }}</h3>
          <p class="text-neutral-300 mb-4">
            {{ $t('settings.botAccounts.authSuccess.message', {username: authUsername}) }}
          </p>
          <button @click="closeModal"
                  class="px-4 py-2 bg-theme-600 hover:bg-theme-700 text-white rounded-md transition-colors">
            {{ $t('common.close') }}
          </button>
        </div>

        <div v-else-if="pollingError" class="text-center">
          <div class="bg-red-500/20 p-4 rounded-full w-16 h-16 mx-auto mb-4 flex items-center justify-center">
            <MoooomIcon icon="alertCircle" class="h-5 w-5"/>
          </div>
          <h3 class="text-xl font-bold text-white mb-2">{{ $t('settings.botAccounts.authFailed.title') }}</h3>
          <p class="text-red-400 mb-4">{{ pollingError }}</p>
          <div class="flex space-x-2 justify-center">
            <button @click="closeModal"
                    class="px-4 py-2 bg-neutral-600 hover:bg-neutral-700 text-white rounded-md transition-colors">
              {{ $t('common.close') }}
            </button>
            <button @click="initiateDeviceCodeFlow"
                    class="px-4 py-2 bg-theme-600 hover:bg-theme-700 text-white rounded-md transition-colors">
              {{ $t('common.tryAgain') }}
            </button>
          </div>
        </div>

        <div v-else>
          <h3 class="text-xl font-bold text-white mb-4">{{ $t('settings.botAccounts.connectBot.title') }}</h3>
          <p class="text-neutral-300 mb-4">
            {{ $t('settings.botAccounts.connectBot.description') }}
          </p>

          <div class="bg-neutral-900 p-4 rounded-md mb-4">
            <a
                :href="`${deviceCodeData?.verification_uri}&public=true`"
                target="_blank"
                class="block text-blue-400 hover:underline mb-2 text-center"
            >
              {{ deviceCodeData?.verification_uri }}&public=true
            </a>

            <p class="text-neutral-400 text-center text-sm mb-2">{{
                $t('settings.botAccounts.connectBot.enterCode')
              }}</p>

            <div class="bg-neutral-700 p-3 text-center rounded-md mb-2">
              <span class="font-mono text-xl tracking-widest text-white">{{ deviceCodeData?.user_code }}</span>
            </div>

            <div class="flex justify-center">
              <button
                  @click="copyToClipboard(`${deviceCodeData?.verification_uri}&public=true`)"
                  class="text-sm text-neutral-400 hover:text-white transition-colors flex items-center"
              >
                <MoooomIcon icon="fileCopy" class="h-5 w-5"/>
                {{ $t('settings.botAccounts.connectBot.copyUrl') }}
              </button>
            </div>
          </div>

          <div class="flex items-center mb-4">
            <div class="h-0.5 flex-grow bg-neutral-700"></div>
            <span class="px-2 text-neutral-500 text-sm">
              {{ isPolling ? 'Waiting for authentication...' : 'Please authenticate' }}
            </span>
            <div class="h-0.5 flex-grow bg-neutral-700"></div>
          </div>

          <div class="text-center">
            <div v-if="isPolling" class="flex items-center justify-center mb-4">
              <div class="animate-spin rounded-full h-6 w-6 border-t-2 border-b-2 border-theme-500"></div>
              <span class="ml-2 text-neutral-300">
                {{ $t('settings.botAccounts.connectBot.waiting') }}
              </span>
            </div>

            <button @click="closeModal"
                    class="px-4 py-2 bg-neutral-600 hover:bg-neutral-700 text-white rounded-md transition-colors">
              {{ $t('common.cancel') }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.text-center {
  text-align: center;
}

.animate-spin {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}
</style>
