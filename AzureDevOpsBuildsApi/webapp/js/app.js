// Main application logic for Azure DevOps Environment Reports Dashboard

class EnvironmentReportsApp {
    constructor() {
        this.autoRefreshTimer = null;
        this.init();
    }

    // Initialize the application
    async init() {
        try {
            // Load saved preferences
            this.loadUserPreferences();
            
            // Setup event listeners
            this.setupEventListeners();
            
            // Initialize API service with configured URL
            const configuredUrl = CONFIG.api.defaultBaseUrl;
            apiService.setBaseUrl(configuredUrl);
            
            // Load initial data
            await this.loadReports();
            
            // Setup auto-refresh if enabled
            this.setupAutoRefresh();
            
            console.log('Environment Reports Dashboard initialized successfully');
        } catch (error) {
            console.error('Failed to initialize application:', error);
            uiManager.showError('Failed to initialize application: ' + error.message);
        }
    }

    // Setup event listeners
    setupEventListeners() {
        // Filter input debouncing
        uiManager.elements.environmentFilter.addEventListener('input', 
            this.debounce(() => this.applyFilters(), CONFIG.ui.debounceDelay)
        );

        // Save filter preferences when they change
        [
            uiManager.elements.pageSizeFilter,
            uiManager.elements.includeVariableGroups
        ].forEach(element => {
            element.addEventListener('change', () => {
                this.saveFilterPreferences();
            });
        });

        // Keyboard shortcuts
        document.addEventListener('keydown', (e) => {
            if (e.ctrlKey || e.metaKey) {
                switch (e.key) {
                    case 'r':
                        e.preventDefault();
                        this.loadReports();
                        break;
                    case 'f':
                        e.preventDefault();
                        uiManager.elements.environmentFilter.focus();
                        break;
                }
            }
        });

        // Handle browser back/forward
        window.addEventListener('popstate', (e) => {
            if (e.state) {
                this.loadReportsFromState(e.state);
            }
        });

        // Handle online/offline status
        window.addEventListener('online', () => {
            uiManager.showNotification('info', 'Connection restored');
            this.loadReports();
        });

        window.addEventListener('offline', () => {
            uiManager.showNotification('warning', 'Connection lost - working offline');
        });
    }

    // Load user preferences from localStorage
    loadUserPreferences() {
        const filters = ConfigUtils.loadPreference('lastFilters');
        if (filters) {
            uiManager.setFilterValues(filters);
        }
    }

    // Save filter preferences to localStorage
    saveFilterPreferences() {
        const filters = uiManager.getFilterValues();
        ConfigUtils.savePreference('lastFilters', filters);
    }

    // Setup auto-refresh functionality
    setupAutoRefresh() {
        if (CONFIG.features.enableAutoRefresh && CONFIG.ui.autoRefreshInterval > 0) {
            this.autoRefreshTimer = setInterval(() => {
                this.loadReports(uiManager.currentPageNumber, false); // Silent refresh
            }, CONFIG.ui.autoRefreshInterval);
        }
    }

    // Stop auto-refresh
    stopAutoRefresh() {
        if (this.autoRefreshTimer) {
            clearInterval(this.autoRefreshTimer);
            this.autoRefreshTimer = null;
        }
    }

    // Debounce utility function
    debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    // Main function to load reports
    async loadReports(pageNumber = 1, showLoading = true) {
        try {
            if (showLoading) {
                uiManager.showLoading();
            }

            const filters = uiManager.getFilterValues();
            const params = {
                environmentName: filters.environmentName || undefined,
                pageNumber: pageNumber,
                pageSize: filters.pageSize,
                includeVariableGroups: filters.includeVariableGroups
            };

            // Update URL state
            this.updateUrlState(params);

            const data = await apiService.getEnvironmentReports(params);
            
            uiManager.currentData = data;
            uiManager.displayReports(data);
            uiManager.updateStats(data);
            uiManager.updatePagination(data);
            uiManager.hideError();

            // Save successful filter combination
            this.saveFilterPreferences();

            if (CONFIG.development.enableDebugMode) {
                console.log('Reports loaded:', data);
            }

        } catch (error) {
            console.error('Error loading reports:', error);
            
            let errorMessage = error.message;
            if (error instanceof ApiError) {
                errorMessage = error.message;
            } else if (!navigator.onLine) {
                errorMessage = 'No internet connection. Please check your network.';
            }
            
            uiManager.showError(errorMessage);
            uiManager.showNotification('error', errorMessage);
        } finally {
            uiManager.hideLoading();
        }
    }

    // Apply filters (reset to page 1)
    async applyFilters() {
        await this.loadReports(1);
    }

    // Clear all filters
    async clearFilters() {
        uiManager.clearFilters();
        await this.loadReports(1);
    }

    // Test API connection
    async testConnection() {
        try {
            uiManager.showNotification('info', 'Testing connection...');
            
            const result = await apiService.testConnection();
            
            if (result.success) {
                uiManager.showNotification('success', result.message);
                return { success: true, message: result.message };
            } else {
                uiManager.showNotification('error', result.message);
                return { success: false, message: result.message };
            }
        } catch (error) {
            const message = `Connection test failed: ${error.message}`;
            uiManager.showNotification('error', message);
            return { success: false, message: message };
        }
    }

    // Update URL state for browser history
    updateUrlState(params) {
        const url = new URL(window.location);
        
        // Clear existing params
        url.search = '';
        
        // Add current params
        Object.entries(params).forEach(([key, value]) => {
            if (value !== undefined && value !== '') {
                url.searchParams.set(key, value);
            }
        });

        // Update browser history
        const state = { params, timestamp: Date.now() };
        window.history.replaceState(state, '', url.toString());
    }

    // Load reports from browser state
    async loadReportsFromState(state) {
        if (state && state.params) {
            // Update UI with state params
            uiManager.setFilterValues(state.params);
            
            // Load reports with state params
            await this.loadReports(state.params.pageNumber || 1);
        }
    }

    // Export current data (if feature enabled)
    exportData(format = 'json') {
        if (!CONFIG.features.enableExport || !uiManager.currentData) {
            uiManager.showNotification('warning', 'Export feature not available');
            return;
        }

        try {
            let exportData;
            let filename;
            let mimeType;

            switch (format.toLowerCase()) {
                case 'json':
                    exportData = JSON.stringify(uiManager.currentData, null, 2);
                    filename = `environment-reports-${new Date().toISOString().split('T')[0]}.json`;
                    mimeType = 'application/json';
                    break;
                
                case 'csv':
                    exportData = this.convertToCSV(uiManager.currentData.data);
                    filename = `environment-reports-${new Date().toISOString().split('T')[0]}.csv`;
                    mimeType = 'text/csv';
                    break;
                
                default:
                    throw new Error('Unsupported export format');
            }

            // Create download link
            const blob = new Blob([exportData], { type: mimeType });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = filename;
            link.click();
            
            URL.revokeObjectURL(url);
            uiManager.showNotification('success', `Data exported as ${filename}`);
            
        } catch (error) {
            console.error('Export failed:', error);
            uiManager.showNotification('error', 'Export failed: ' + error.message);
        }
    }

    // Convert data to CSV format
    convertToCSV(data) {
        if (!data || data.length === 0) return '';

        const headers = [
            'Environment Name',
            'Environment ID',
            'Stage Name',
            'Result',
            'Finish Time',
            'Build Number',
            'Build Status',
            'Source Branch',
            'Modified By'
        ];

        const rows = data.map(report => [
            report.environmentName,
            report.environmentId,
            report.deploymentRecordStageName,
            report.deploymentRecordResult,
            report.deploymentRecordFinishTime,
            report.buildNumber,
            report.buildStatus,
            report.buildSourceBranch,
            report.environmentLastModifiedBy
        ]);

        const csvContent = [headers, ...rows]
            .map(row => row.map(field => `"${(field || '').toString().replace(/"/g, '""')}"`).join(','))
            .join('\n');

        return csvContent;
    }

    // Handle application errors globally
    handleError(error, context = 'Application') {
        console.error(`${context} Error:`, error);
        
        let userMessage = 'An unexpected error occurred';
        
        if (error instanceof ApiError) {
            userMessage = error.message;
        } else if (error.name === 'NetworkError') {
            userMessage = 'Network connection error. Please check your internet connection.';
        } else if (error.message) {
            userMessage = error.message;
        }

        uiManager.showError(userMessage);
        uiManager.showNotification('error', userMessage);
    }

    // Cleanup on page unload
    cleanup() {
        this.stopAutoRefresh();
        apiService.cancelRequests();
    }
}

// Global functions for HTML onclick handlers
window.loadReports = async (pageNumber = 1) => {
    await app.loadReports(pageNumber);
};

window.applyFilters = async () => {
    await app.applyFilters();
};

window.clearFilters = async () => {
    await app.clearFilters();
};

// Initialize application when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    // Create global app instance
    window.app = new EnvironmentReportsApp();
    
    // Handle page unload
    window.addEventListener('beforeunload', () => {
        window.app.cleanup();
    });
    
    // Handle visibility change (pause/resume auto-refresh)
    document.addEventListener('visibilitychange', () => {
        if (CONFIG.features.enableAutoRefresh) {
            if (document.hidden) {
                window.app.stopAutoRefresh();
            } else {
                window.app.setupAutoRefresh();
            }
        }
    });
});

// Global error handler
window.addEventListener('error', (event) => {
    console.error('Global error:', event.error);
    if (window.app) {
        window.app.handleError(event.error, 'Global');
    }
});

// Handle unhandled promise rejections
window.addEventListener('unhandledrejection', (event) => {
    console.error('Unhandled promise rejection:', event.reason);
    if (window.app) {
        window.app.handleError(event.reason, 'Promise');
    }
    event.preventDefault();
});