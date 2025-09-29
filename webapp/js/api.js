// API service for Azure DevOps Environment Reports

class ApiService {
    constructor() {
        this.baseUrl = CONFIG.api.defaultBaseUrl;
        this.timeout = CONFIG.api.timeout;
        this.retryAttempts = CONFIG.api.retryAttempts;
        this.cache = new Map();
        this.abortController = null;
    }

    // Set the base URL for API calls
    setBaseUrl(url) {
        this.baseUrl = url.replace(/\/+$/, ''); // Remove trailing slashes
        // Save to localStorage
        localStorage.setItem(CONFIG.storage.prefix + CONFIG.storage.keys.apiBaseUrl, this.baseUrl);
    }

    // Get the current base URL
    getBaseUrl() {
        const saved = localStorage.getItem(CONFIG.storage.prefix + CONFIG.storage.keys.apiBaseUrl);
        return saved || this.baseUrl;
    }

    // Create abort controller for request cancellation
    createAbortController() {
        if (this.abortController) {
            this.abortController.abort();
        }
        this.abortController = new AbortController();
        return this.abortController;
    }

    // Generic fetch wrapper with error handling and retries
    async fetchWithRetry(url, options = {}, attempt = 1) {
        const controller = this.createAbortController();
        
        const fetchOptions = {
            ...options,
            signal: controller.signal,
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json',
                ...options.headers
            }
        };

        // Add timeout
        const timeoutId = setTimeout(() => {
            controller.abort();
        }, this.timeout);

        try {
            const response = await fetch(url, fetchOptions);
            clearTimeout(timeoutId);

            if (!response.ok) {
                throw new ApiError(
                    this.getErrorMessage(response.status),
                    response.status,
                    response.statusText
                );
            }

            const data = await response.json();
            return data;

        } catch (error) {
            clearTimeout(timeoutId);

            // Handle abort
            if (error.name === 'AbortError') {
                throw new ApiError(CONFIG.messages.errors.timeoutError, 408, 'Timeout');
            }

            // Handle network errors
            if (!window.navigator.onLine) {
                throw new ApiError('No internet connection', 0, 'Network Error');
            }

            // Retry logic
            if (attempt < this.retryAttempts && this.shouldRetry(error)) {
                console.warn(`API request failed, retrying (${attempt}/${this.retryAttempts})...`);
                await this.delay(1000 * attempt); // Exponential backoff
                return this.fetchWithRetry(url, options, attempt + 1);
            }

            // Re-throw original error or create new one
            if (error instanceof ApiError) {
                throw error;
            }
            
            throw new ApiError(
                CONFIG.messages.errors.networkError,
                0,
                error.message
            );
        }
    }

    // Determine if error should trigger a retry
    shouldRetry(error) {
        // Retry on network errors and specific HTTP status codes
        return !error.status || error.status >= 500 || error.status === 408 || error.status === 429;
    }

    // Delay utility for retries
    delay(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    // Get error message based on status code
    getErrorMessage(status) {
        switch (status) {
            case 400:
                return CONFIG.messages.errors.validationError;
            case 401:
                return CONFIG.messages.errors.unauthorizedError;
            case 403:
                return CONFIG.messages.errors.forbiddenError;
            case 404:
                return CONFIG.messages.errors.notFoundError;
            case 408:
                return CONFIG.messages.errors.timeoutError;
            case 500:
            case 502:
            case 503:
            case 504:
                return CONFIG.messages.errors.serverError;
            default:
                return CONFIG.messages.errors.unknownError;
        }
    }

    // Generate cache key for requests
    generateCacheKey(url, params) {
        const sortedParams = Object.keys(params || {})
            .sort()
            .map(key => `${key}=${params[key]}`)
            .join('&');
        return `${url}?${sortedParams}`;
    }

    // Get cached data if available and not expired
    getCachedData(cacheKey) {
        if (!CONFIG.performance.cacheEnabled) return null;
        
        const cached = this.cache.get(cacheKey);
        if (cached && Date.now() - cached.timestamp < CONFIG.performance.cacheExpiry) {
            return cached.data;
        }
        
        if (cached) {
            this.cache.delete(cacheKey);
        }
        
        return null;
    }

    // Cache data with timestamp
    setCachedData(cacheKey, data) {
        if (!CONFIG.performance.cacheEnabled) return;
        
        this.cache.set(cacheKey, {
            data,
            timestamp: Date.now()
        });
    }

    // Clear cache
    clearCache() {
        this.cache.clear();
    }

    // Main method to get environment reports
    async getEnvironmentReports(params = {}) {
        const baseUrl = this.getBaseUrl();
        const endpoint = CONFIG.api.endpoints.environmentReport;
        
        // Build query parameters
        const queryParams = new URLSearchParams();
        
        if (params.environmentName) {
            queryParams.append('environmentName', params.environmentName);
        }
        if (params.result) {
            queryParams.append('result', params.result);
        }
        if (params.pageNumber) {
            queryParams.append('pageNumber', params.pageNumber);
        }
        if (params.pageSize) {
            queryParams.append('pageSize', params.pageSize);
        }
        if (params.includeVariableGroups !== undefined) {
            queryParams.append('includeVariableGroups', params.includeVariableGroups);
        }

        const url = `${baseUrl}${endpoint}?${queryParams.toString()}`;
        const cacheKey = this.generateCacheKey(url, params);
        
        // Check cache first
        const cachedData = this.getCachedData(cacheKey);
        if (cachedData) {
            return cachedData;
        }

        try {
            const data = await this.fetchWithRetry(url);
            
            // Cache successful response
            this.setCachedData(cacheKey, data);
            
            return data;
        } catch (error) {
            console.error('Error fetching environment reports:', error);
            throw error;
        }
    }

    // Test API connection
    async testConnection() {
        const baseUrl = this.getBaseUrl();
        const endpoint = CONFIG.api.endpoints.environmentReport;
        const url = `${baseUrl}${endpoint}?pageSize=1`;

        try {
            await this.fetchWithRetry(url);
            return { success: true, message: CONFIG.messages.success.connectionTest };
        } catch (error) {
            return { 
                success: false, 
                message: error.message,
                status: error.status 
            };
        }
    }

    // Cancel ongoing requests
    cancelRequests() {
        if (this.abortController) {
            this.abortController.abort();
            this.abortController = null;
        }
    }

    // Get API health/status
    async getApiHealth() {
        const baseUrl = this.getBaseUrl();
        try {
            const response = await fetch(`${baseUrl}/health`, {
                method: 'GET',
                signal: AbortSignal.timeout(5000)
            });
            return response.ok;
        } catch {
            return false;
        }
    }
}

// Custom error class for API errors
class ApiError extends Error {
    constructor(message, status, statusText) {
        super(message);
        this.name = 'ApiError';
        this.status = status;
        this.statusText = statusText;
    }
}

// Create global API service instance
const apiService = new ApiService();