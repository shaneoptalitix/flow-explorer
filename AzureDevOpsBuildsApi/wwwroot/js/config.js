// Configuration file for the Azure DevOps Environment Reports Dashboard

const CONFIG = {
    // API Configuration - Empty baseUrl means same origin (same server)
    api: {
        defaultBaseUrl: '',  // Changed from 'https://localhost:8443' to '' for same-origin
        endpoints: {
            environmentReport: '/api/EnvironmentReport'
        },
        timeout: 30000, // 30 seconds
        retryAttempts: 3
    },
    
    // UI Configuration
    ui: {
        defaultPageSize: 10,
        maxPageSize: 100,
        debounceDelay: 500, // milliseconds for search input debouncing
        autoRefreshInterval: 300000, // 5 minutes in milliseconds (0 to disable)
        animations: {
            enabled: true,
            duration: 300
        }
    },
    
    // Filter Configuration
    filters: {
        defaultIncludeVariableGroups: false,  // Changed default to false
        availablePageSizes: [10, 25, 50, 100]
    },
    
    // Status Configuration
    status: {
        colors: {
            succeeded: {
                bg: 'bg-green-100',
                text: 'text-green-800',
                icon: 'fas fa-check-circle',
                dot: 'success'
            },
            failed: {
                bg: 'bg-red-100',
                text: 'text-red-800',
                icon: 'fas fa-times-circle',
                dot: 'failure'
            },
            partiallySucceeded: {
                bg: 'bg-yellow-100',
                text: 'text-yellow-800',
                icon: 'fas fa-exclamation-triangle',
                dot: 'warning'
            },
            canceled: {
                bg: 'bg-gray-100',
                text: 'text-gray-800',
                icon: 'fas fa-ban',
                dot: 'neutral'
            },
            default: {
                bg: 'bg-gray-100',
                text: 'text-gray-800',
                icon: 'fas fa-question-circle',
                dot: 'neutral'
            }
        }
    },
    
    // Feature Flags
    features: {
        enableAutoRefresh: false,
        enableDarkMode: false,
        enableNotifications: false,
        enableExport: false,
        enableAdvancedFilters: false
    },
    
    // Storage Configuration
    storage: {
        prefix: 'azdo_env_dashboard_',
        keys: {
            apiBaseUrl: 'api_base_url',
            userPreferences: 'user_preferences',
            lastFilters: 'last_filters'
        }
    },
    
    // Notification Configuration
    notifications: {
        position: 'top-right',
        duration: 5000, // milliseconds
        types: {
            success: { icon: 'fas fa-check-circle', color: 'green' },
            error: { icon: 'fas fa-exclamation-circle', color: 'red' },
            warning: { icon: 'fas fa-exclamation-triangle', color: 'yellow' },
            info: { icon: 'fas fa-info-circle', color: 'blue' }
        }
    },
    
    // Date/Time Configuration
    dateTime: {
        locale: 'en-US',
        options: {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        },
        timezone: 'local' // or 'UTC'
    },
    
    // Theme Configuration
    theme: {
        colors: {
            primary: '#0078d4',
            primaryDark: '#004578',
            success: '#10b981',
            warning: '#f59e0b',
            error: '#ef4444',
            info: '#3b82f6'
        }
    },
    
    // Error Messages
    messages: {
        errors: {
            networkError: 'Unable to connect to the API. Please check your connection and API URL.',
            timeoutError: 'Request timed out. Please try again.',
            notFoundError: 'API endpoint not found. Please check your API URL.',
            serverError: 'Server error occurred. Please try again later.',
            unauthorizedError: 'Unauthorized access. Please check your credentials.',
            forbiddenError: 'Access forbidden. Please check your permissions.',
            validationError: 'Invalid request parameters.',
            unknownError: 'An unknown error occurred. Please try again.'
        },
        success: {
            dataLoaded: 'Environment reports loaded successfully',
            connectionTest: 'API connection test successful',
            filtersApplied: 'Filters applied successfully'
        },
        warnings: {
            noData: 'No environment reports found with current filters',
            slowConnection: 'API response is slower than expected',
            partialData: 'Some data could not be loaded completely'
        }
    },
    
    // Performance Configuration
    performance: {
        enableVirtualScrolling: false, // For large datasets
        cacheEnabled: true,
        cacheExpiry: 300000, // 5 minutes
        lazyLoadImages: true,
        prefetchNextPage: false
    },
    
    // Development Configuration
    development: {
        enableDebugMode: false,
        logLevel: 'info', // 'debug', 'info', 'warn', 'error'
        mockData: false,
        enablePerformanceMetrics: false
    }
};

// Tailwind configuration for dynamic classes
if (typeof tailwind !== 'undefined') {
    tailwind.config = {
        theme: {
            extend: {
                colors: {
                    'azure': CONFIG.theme.colors.primary,
                    'azure-dark': CONFIG.theme.colors.primaryDark
                },
                animation: {
                    'pulse-azure': 'pulse-azure 2s cubic-bezier(0.4, 0, 0.6, 1) infinite',
                    'fade-in': 'fadeIn 0.5s ease-in'
                }
            }
        }
    };
}

// Utility functions for configuration
const ConfigUtils = {
    // Get configuration value with fallback
    get: (path, fallback = null) => {
        const keys = path.split('.');
        let value = CONFIG;
        
        for (const key of keys) {
            if (value && typeof value === 'object' && key in value) {
                value = value[key];
            } else {
                return fallback;
            }
        }
        
        return value;
    },
    
    // Check if feature is enabled
    isFeatureEnabled: (featureName) => {
        return ConfigUtils.get(`features.${featureName}`, false);
    },
    
    // Get status configuration for a result
    getStatusConfig: (result) => {
        const normalizedResult = result?.toLowerCase();
        return CONFIG.status.colors[normalizedResult] || CONFIG.status.colors.default;
    },
    
    // Get API URL with endpoint
    getApiUrl: (endpoint = '') => {
        // Use stored base URL or default (empty for same origin)
        const baseUrl = localStorage.getItem(CONFIG.storage.prefix + CONFIG.storage.keys.apiBaseUrl) 
                       || CONFIG.api.defaultBaseUrl;
        return baseUrl + endpoint;
    },
    
    // Save user preference
    savePreference: (key, value) => {
        try {
            const preferences = JSON.parse(localStorage.getItem(CONFIG.storage.prefix + CONFIG.storage.keys.userPreferences) || '{}');
            preferences[key] = value;
            localStorage.setItem(CONFIG.storage.prefix + CONFIG.storage.keys.userPreferences, JSON.stringify(preferences));
        } catch (error) {
            console.warn('Failed to save user preference:', error);
        }
    },
    
    // Load user preference
    loadPreference: (key, fallback = null) => {
        try {
            const preferences = JSON.parse(localStorage.getItem(CONFIG.storage.prefix + CONFIG.storage.keys.userPreferences) || '{}');
            return preferences[key] !== undefined ? preferences[key] : fallback;
        } catch (error) {
            console.warn('Failed to load user preference:', error);
            return fallback;
        }
    },
    
    // Format date according to configuration
    formatDate: (date) => {
        if (!date) return 'N/A';
        try {
            return new Date(date).toLocaleString(CONFIG.dateTime.locale, CONFIG.dateTime.options);
        } catch (error) {
            return date.toString();
        }
    }
};

// Export for use in other modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { CONFIG, ConfigUtils };
}