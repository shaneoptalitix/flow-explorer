// UI management for Azure DevOps Environment Reports Dashboard

class UIManager {
    constructor() {
        this.currentPageNumber = 1;
        this.totalPages = 1;
        this.currentData = null;
        this.elements = this.initializeElements();
    }

    // Initialize DOM element references
    initializeElements() {
        return {
            // Filters
            environmentFilter: document.getElementById('environmentFilter'),
            pageSizeFilter: document.getElementById('pageSizeFilter'),
            includeVariableGroups: document.getElementById('includeVariableGroups'),
            
            // Stats
            totalEnvironments: document.getElementById('totalEnvironments'),
            lastUpdated: document.getElementById('lastUpdated'),
            
            // States
            loadingState: document.getElementById('loadingState'),
            errorState: document.getElementById('errorState'),
            errorMessage: document.getElementById('errorMessage'),
            reportsContainer: document.getElementById('reportsContainer'),
            paginationContainer: document.getElementById('paginationContainer'),
            
            // Icons
            refreshIcon: document.getElementById('refreshIcon')
        };
    }

    // Show loading state
    showLoading() {
        this.elements.loadingState.classList.remove('hidden');
        this.elements.reportsContainer.classList.add('hidden');
        this.elements.errorState.classList.add('hidden');
        this.elements.refreshIcon.classList.add('animate-spin');
    }

    // Hide loading state
    hideLoading() {
        this.elements.loadingState.classList.add('hidden');
        this.elements.reportsContainer.classList.remove('hidden');
        this.elements.refreshIcon.classList.remove('animate-spin');
    }

    // Show error state
    showError(message) {
        this.elements.errorState.classList.remove('hidden');
        this.elements.errorMessage.textContent = message;
        this.elements.reportsContainer.classList.add('hidden');
        this.elements.loadingState.classList.add('hidden');
    }

    // Hide error state
    hideError() {
        this.elements.errorState.classList.add('hidden');
    }

    // Update statistics
    updateStats(data) {
        this.elements.totalEnvironments.textContent = data.totalCount || 0;
        
        // Update last updated timestamp
        this.elements.lastUpdated.textContent = 
            `Last updated: ${ConfigUtils.formatDate(new Date())}`;
    }

    // Display reports in the container
    displayReports(data) {
        if (!data.data || data.data.length === 0) {
            this.elements.reportsContainer.innerHTML = this.createEmptyState();
            return;
        }
        
        const reportsHtml = data.data.map(report => this.createReportCard(report)).join('');
        this.elements.reportsContainer.innerHTML = reportsHtml;
        
        // Add fade-in animation if enabled
        if (CONFIG.ui.animations.enabled) {
            this.elements.reportsContainer.classList.add('fade-in');
        }
    }

    // Create empty state HTML
    createEmptyState() {
        return `
            <div class="bg-white rounded-lg shadow-sm p-12 text-center">
                <i class="fas fa-inbox text-gray-400 text-4xl mb-4"></i>
                <h3 class="text-lg font-medium text-gray-900 mb-2">No reports found</h3>
                <p class="text-gray-500">Try adjusting your filters or check your API connection.</p>
            </div>
        `;
    }

    // Create individual report card HTML
    createReportCard(report) {
        const finishTime = ConfigUtils.formatDate(report.deploymentRecordFinishTime);
        const startTime = ConfigUtils.formatDate(report.buildStartTime);
        
        const hasVariableGroups = report.variableGroupVariables && 
            Object.keys(report.variableGroupVariables).length > 0;
        
        // Check for LoadBalancer.Domain variable to create link
        const loadBalancerDomain = hasVariableGroups ? 
            report.variableGroupVariables['LoadBalancer.Domain'] : null;
        
        const environmentNameHtml = loadBalancerDomain && loadBalancerDomain !== '[HIDDEN]' ?
            `<a href="https://${this.escapeHtml(loadBalancerDomain)}" target="_blank" rel="noopener noreferrer" 
                class="text-xl font-semibold text-azure hover:text-azure-dark transition-colors underline decoration-dotted">
                ${this.escapeHtml(report.environmentName)}
                <i class="fas fa-external-link-alt ml-1 text-sm"></i>
             </a>` :
            `<h3 class="text-xl font-semibold text-gray-900">${this.escapeHtml(report.environmentName)}</h3>`;
        
        return `
            <div class="bg-white rounded-lg shadow-sm border border-gray-200 hover:shadow-md transition-shadow report-card">
                <div class="p-6">
                    <!-- Header -->
                    <div class="mb-4">
                        ${environmentNameHtml}
                        <p class="text-sm text-gray-500">Environment ID: ${report.environmentId}</p>
                    </div>
                    
                    <!-- Grid Layout -->
                    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
                        <!-- Deployment Info -->
                        <div>
                            <h4 class="font-medium text-gray-900 mb-3 flex items-center">
                                <i class="fas fa-rocket mr-2 text-blue-500"></i>
                                Deployment Details
                            </h4>
                            <div class="space-y-2 text-sm">
                                <div class="flex justify-between">
                                    <span class="text-gray-500">Stage:</span>
                                    <span class="font-medium">${this.escapeHtml(report.deploymentRecordStageName)}</span>
                                </div>
                                <div class="flex justify-between">
                                    <span class="text-gray-500">Pipeline:</span>
                                    <span class="font-medium">${this.escapeHtml(report.deploymentRecordDefinitionName)}</span>
                                </div>
                                <div class="flex justify-between">
                                    <span class="text-gray-500">Finished:</span>
                                    <span class="font-medium">${finishTime}</span>
                                </div>
                            </div>
                        </div>
                        
                        <!-- Build Info -->
                        <div>
                            <h4 class="font-medium text-gray-900 mb-3 flex items-center">
                                <i class="fas fa-code-branch mr-2 text-green-500"></i>
                                Build Information
                            </h4>
                            <div class="space-y-2 text-sm">
                                <div class="flex justify-between">
                                    <span class="text-gray-500">Build #:</span>
                                    <span class="font-medium font-mono">${this.escapeHtml(report.buildNumber) || 'N/A'}</span>
                                </div>
                                <div class="flex justify-between">
                                    <span class="text-gray-500">Repository:</span>
                                    <span class="font-medium">${this.escapeHtml(report.buildTriggerRepository) || 'N/A'}</span>
                                </div>
                                <div class="flex justify-between">
                                    <span class="text-gray-500">Branch:</span>
                                    <span class="font-medium font-mono text-xs">${this.escapeHtml(this.truncateText(report.buildSourceBranch, 30)) || 'N/A'}</span>
                                </div>
                                <div class="flex justify-between">
                                    <span class="text-gray-500">Source Version:</span>
                                    <span class="font-medium font-mono text-xs">${this.escapeHtml(this.truncateText(report.buildSourceVersion, 12)) || 'N/A'}</span>
                                </div>
                                <div class="flex justify-between">
                                    <span class="text-gray-500">Started:</span>
                                    <span class="font-medium">${startTime}</span>
                                </div>
                            </div>
                        </div>
                    </div>
                    
                    <!-- Variable Groups (if available) -->
                    ${hasVariableGroups ? this.createVariableGroupsSection(report) : ''}
                </div>
            </div>
        `;
    }

    // Create variable groups section
    createVariableGroupsSection(report) {
        return `
            <div class="mt-6 pt-6 border-t border-gray-200">
                <h4 class="font-medium text-gray-900 mb-3 flex items-center">
                    <i class="fas fa-cog mr-2 text-purple-500"></i>
                    Variable Group: ${this.escapeHtml(report.variableGroupName)}
                </h4>
                <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
                    ${Object.entries(report.variableGroupVariables).map(([key, value]) => `
                        <div class="variable-item ${value === '[HIDDEN]' ? 'secret' : 'normal'} rounded-lg p-3">
                            <div class="text-xs font-medium text-gray-500 uppercase tracking-wide">${this.escapeHtml(key)}</div>
                            <div class="text-sm font-mono mt-1 ${value === '[HIDDEN]' ? 'text-red-600' : 'text-gray-900'}">${this.escapeHtml(value)}</div>
                        </div>
                    `).join('')}
                </div>
            </div>
        `;
    }

    // Create repository section
    createRepositorySection(report) {
        return `
            <div class="mt-4 pt-4 border-t border-gray-200">
                <div class="flex items-center text-sm text-gray-500">
                    <i class="fab fa-git-alt mr-2"></i>
                    <span>Repository: ${this.escapeHtml(report.buildTriggerRepository)}</span>
                    ${report.buildTriggerMessage ? `
                        <span class="ml-4">• ${this.escapeHtml(this.truncateText(report.buildTriggerMessage, 50))}</span>
                    ` : ''}
                </div>
            </div>
        `;
    }

    // Update pagination
    updatePagination(data) {
        if (data.totalPages <= 1) {
            this.elements.paginationContainer.innerHTML = '';
            return;
        }
        
        this.currentPageNumber = data.pageNumber;
        this.totalPages = data.totalPages;
        
        let paginationHtml = `
            <div class="bg-white px-4 py-3 flex items-center justify-between border-t border-gray-200 sm:px-6 rounded-lg shadow-sm">
                <div class="flex-1 flex justify-between sm:hidden">
                    <button onclick="uiManager.loadPage(${data.pageNumber - 1})" 
                            ${!data.hasPreviousPage ? 'disabled' : ''} 
                            class="relative inline-flex items-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 ${!data.hasPreviousPage ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}">
                        Previous
                    </button>
                    <button onclick="uiManager.loadPage(${data.pageNumber + 1})" 
                            ${!data.hasNextPage ? 'disabled' : ''} 
                            class="ml-3 relative inline-flex items-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 ${!data.hasNextPage ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}">
                        Next
                    </button>
                </div>
                <div class="hidden sm:flex-1 sm:flex sm:items-center sm:justify-between">
                    <div>
                        <p class="text-sm text-gray-700">
                            Showing page <span class="font-medium">${data.pageNumber}</span> of <span class="font-medium">${data.totalPages}</span>
                            (<span class="font-medium">${data.totalCount}</span> total results)
                        </p>
                    </div>
                    <div>
                        <nav class="relative z-0 inline-flex rounded-md shadow-sm -space-x-px">
        `;
        
        // Previous button
        paginationHtml += `
            <button onclick="uiManager.loadPage(${data.pageNumber - 1})" 
                    ${!data.hasPreviousPage ? 'disabled' : ''} 
                    class="relative inline-flex items-center px-2 py-2 rounded-l-md border border-gray-300 bg-white text-sm font-medium text-gray-500 hover:bg-gray-50 ${!data.hasPreviousPage ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}">
                <i class="fas fa-chevron-left"></i>
            </button>
        `;
        
        // Page numbers
        const startPage = Math.max(1, data.pageNumber - 2);
        const endPage = Math.min(data.totalPages, data.pageNumber + 2);
        
        for (let i = startPage; i <= endPage; i++) {
            const isActive = i === data.pageNumber;
            paginationHtml += `
                <button onclick="uiManager.loadPage(${i})" 
                        class="relative inline-flex items-center px-4 py-2 border ${isActive ? 'border-azure bg-azure text-white' : 'border-gray-300 bg-white text-gray-700 hover:bg-gray-50 cursor-pointer'} text-sm font-medium">
                    ${i}
                </button>
            `;
        }
        
        // Next button
        paginationHtml += `
            <button onclick="uiManager.loadPage(${data.pageNumber + 1})" 
                    ${!data.hasNextPage ? 'disabled' : ''} 
                    class="relative inline-flex items-center px-2 py-2 rounded-r-md border border-gray-300 bg-white text-sm font-medium text-gray-500 hover:bg-gray-50 ${!data.hasNextPage ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}">
                <i class="fas fa-chevron-right"></i>
            </button>
        `;
        
        paginationHtml += `
                        </nav>
                    </div>
                </div>
            </div>
        `;
        
        this.elements.paginationContainer.innerHTML = paginationHtml;
    }

    // Load specific page
    async loadPage(pageNumber) {
        if (pageNumber < 1 || pageNumber > this.totalPages) return;
        await window.loadReports(pageNumber);
    }

    // Get current filter values
    getFilterValues() {
        return {
            environmentName: this.elements.environmentFilter.value,
            pageSize: parseInt(this.elements.pageSizeFilter.value),
            includeVariableGroups: this.elements.includeVariableGroups.value === 'true'
        };
    }

    // Set filter values
    setFilterValues(filters) {
        if (filters.environmentName !== undefined) {
            this.elements.environmentFilter.value = filters.environmentName;
        }
        if (filters.pageSize !== undefined) {
            this.elements.pageSizeFilter.value = filters.pageSize;
        }
        if (filters.includeVariableGroups !== undefined) {
            this.elements.includeVariableGroups.value = filters.includeVariableGroups;
        }
    }

    // Clear all filters
    clearFilters() {
        this.elements.environmentFilter.value = '';
        this.elements.pageSizeFilter.value = CONFIG.ui.defaultPageSize;
        this.elements.includeVariableGroups.value = CONFIG.filters.defaultIncludeVariableGroups;
    }

    // Utility functions
    escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    truncateText(text, maxLength) {
        if (!text || text.length <= maxLength) return text;
        return text.substring(0, maxLength) + '...';
    }

    // Show notification (if notifications are enabled)
    showNotification(type, message) {
        if (!CONFIG.features.enableNotifications) return;
        
        // Implementation for notifications would go here
        console.log(`${type.toUpperCase()}: ${message}`);
    }
}

// Create global UI manager instance
const uiManager = new UIManager();