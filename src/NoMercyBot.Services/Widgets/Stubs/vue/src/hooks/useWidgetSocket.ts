import type { HubConnection } from '@microsoft/signalr';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { onUnmounted, reactive } from 'vue';

export interface WidgetConnection {
    state: {
        connection: HubConnection | null;
        isConnected: boolean;
    };
    connect: () => Promise<void>;
    disconnect: () => Promise<void>;
    on: (methodName: string, callback: (...args: any[]) => void) => void;
    off: (methodName: string, callback?: (...args: any[]) => void) => void;
    invoke: (methodName: string, ...args: any[]) => Promise<any>;
}

/**
 * Generic WebSocket hook for widget connections
 * Usage:
 *
 * import { useWidgetSocket } from './hooks/useWidgetSocket';
 *
 * const socket = useWidgetSocket('01KCC89B7Z14M1Z0QEC8PDAGZB');
 *
 * // Connect to widget hub
 * await socket.connect();
 *
 * // Listen for events
 * socket.on('ChatMessage', (data) => {
 *   console.log('Received:', data);
 * });
 *
 * // Send messages
 * await socket.invoke('SomeMethod', { data: 'example' });
 */

const widgetSocketInstances: Record<string, WidgetConnection> = {};

function createWidgetSocket(widgetId: string): WidgetConnection {
    const state = reactive({
        connection: null as HubConnection | null,
        isConnected: false,
    });

    const connect = async () => {
        if (state.connection?.state === 'Connected') {
            return;
        }

        try {
            state.connection = new HubConnectionBuilder()
                .withUrl(`/hubs/widgets?widgetId=${widgetId}`)
                .withAutomaticReconnect()
                .configureLogging(LogLevel.Information)
                .build();

            state.connection.onreconnecting(() => {
                state.isConnected = false;
                console.log('Widget socket reconnecting...');
            });

            state.connection.onreconnected(async () => {
                state.isConnected = true;
                await state.connection?.invoke('JoinWidgetGroup', widgetId);
                console.log('Widget socket reconnected');
            });

            // Listen for server shutdown notifications
            state.connection.on('ServerShutdown', () => {
                console.log('Server is shutting down - marking as disconnected');
                state.isConnected = false;
            });

            // Also listen for connection close events
            state.connection.onclose((error) => {
                console.log('Widget socket disconnected:', error);
                state.isConnected = false;
            });

            await state.connection.start();
            await state.connection.invoke('JoinWidgetGroup', widgetId);
            state.isConnected = true;
        }
        catch (error) {
            console.error('Failed to connect to widget socket:', error);
            state.isConnected = false;
        }
    };

    const disconnect = async () => {
        if (state.connection) {
            // Leave widget group before disconnecting
            try {
                await state.connection.invoke('LeaveWidgetGroup', widgetId);
            }
            catch (error) {
                console.warn('Failed to leave widget group:', error);
            }

            await state.connection.stop();
        }
        state.connection = null;
        state.isConnected = false;
    };

    const on = (methodName: string, callback: (...args: any[]) => void) => {
        state.connection?.on(methodName, callback);
    };

    const off = (methodName: string, callback?: (...args: any[]) => void) => {
        if (callback) {
            state.connection?.off(methodName, callback);
        }
        else {
            state.connection?.off(methodName);
        }
    };

    const invoke = async (methodName: string, ...args: any[]) => {
        if (state.connection?.state === 'Connected') {
            return await state.connection.invoke(methodName, ...args);
        }
        throw new Error('Connection not established');
    };

    // Auto cleanup on component unmount
    onUnmounted(() => {
        disconnect().then();
    });

    return <WidgetConnection>{
        state,
        connect,
        disconnect,
        on,
        off,
        invoke,
    };
}

export function useWidgetSocket(widgetId: string) {
    if (!widgetSocketInstances[widgetId]) {
        widgetSocketInstances[widgetId] = createWidgetSocket(widgetId);
    }
    return widgetSocketInstances[widgetId];
}
