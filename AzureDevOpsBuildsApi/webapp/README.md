# Azure DevOps Environment Reports Dashboard

A modern, responsive web application for viewing Azure DevOps environment deployment reports with advanced filtering, pagination, and real-time updates.

## 📁 Project Structure

```
webapp/
├── index.html              # Main HTML file
├── css/
│   └── styles.css          # Custom CSS styles and Tailwind extensions
├── js/
│   ├── config.js           # Configuration and constants
│   ├── api.js              # API service layer
│   ├── ui.js               # UI management and DOM manipulation
│   └── app.js              # Main application logic
└── README.md               # This file
```

## 🚀 Features

### Core Functionality
- **Environment Reports Display**: View deployment reports in beautiful card format
- **Advanced Filtering**: Filter by environment name, result status, and more
- **Pagination**: Navigate through large datasets efficiently
- **Real-time Updates**: Auto-refresh capabilities with configurable intervals
- **API Configuration**: Dynamic API endpoint configuration with connection testing

### UI/UX Features
- **Responsive Design**: Works seamlessly on desktop, tablet, and mobile
- **Modern Interface**: Clean, Azure-themed design with Tailwind CSS
- **Interactive Elements**: Hover effects, animations, and smooth transitions
- **Status Indicators**: Color-coded deployment status with icons
- **Variable Groups**: Display environment variables with secret masking
- **Loading States**: Professional loading indicators and error handling

### Technical Features
- **Modular Architecture**: Separated concerns with dedicated files
- **Error Handling**: Comprehensive error handling with user-friendly messages
- **Caching**: Smart caching to reduce API calls and improve performance
- **Keyboard Shortcuts**: Ctrl+R to refresh, Ctrl+F to focus search
- **Browser History**: URL state management for bookmarking and sharing
- **Local Storage**: Persistent user preferences and settings

## 🛠 Setup Instructions

### 1. File Deployment
```bash
# Create project directory
mkdir azure-devops-dashboard
cd azure-devops-dashboard

# Create the folder structure
mkdir css js

# Copy all files to their respective directories:
# - index.html (root)
# - css/styles.css
# - js/config.js
# - js/api.js
# - js/ui.js
# - js/app.js
```

### 2. Web Server Setup
The application requires a web server due to CORS restrictions. Choose one of these options:

#### Option A: Python HTTP Server
```bash
# Python 3
python -m http.server 8000

# Python 2
python -m SimpleHTTPServer 8000

# Access at: http://localhost:8000
```

#### Option B: Node.js HTTP Server
```bash
# Install http-server globally
npm install -g http-server

# Run server
http-server -p 8000

# Access at: http://localhost:8000
```

#### Option C: PHP Built-in Server
```bash
php -S localhost:8000

# Access at: http://localhost:8000
```

#### Option D: Live Server (VS Code Extension)
1. Install "Live Server" extension in VS Code
2. Right-click `index.html` → "Open with Live Server"

### 3. API Configuration
1. Open the web application in your browser
2. In the "API Configuration" section, update the "API Base URL" field
3. Default: `http://localhost:8080` (update if your API runs elsewhere)
4. Click "Test Connection" to verify connectivity

## ⚙️ Configuration

### API Settings (js/config.js)
```javascript
api: {
    defaultBaseUrl: 'http://localhost:8080',  // Change this to your API URL
    timeout: 30000,                          // Request timeout in milliseconds
    retryAttempts: 3                         // Number of retry attempts
}
```

### UI Customization
```javascript
ui: {
    defaultPageSize: 10,                     // Default items per page
    debounceDelay: 500,                      // Search input delay
    autoRefreshInterval: 300000,             // Auto-refresh interval (5 min)
    animations: { enabled: true }            // Enable/disable animations
}
```

### Feature Flags
```javascript
features: {
    enableAutoRefresh: false,                // Auto-refresh functionality
    enableNotifications: false,              // Toast notifications
    enableExport: false,                     // Data export features
    enableDarkMode: false                    // Dark mode support
}
```

## 🎨 Customization

### Changing Colors
Update the CSS variables in `css/styles.css`:
```css
:root {
    --azure-blue: #0078d4;        /* Primary color */
    --azure-blue-dark: #004578;   /* Dark variant */
}
```

### Adding Custom Styles
Add your custom CSS to `css/styles.css` after the existing rules.

### Modifying Layout
Edit the HTML structure in `index.html` and corresponding JavaScript in `js/ui.js`.

## 🔧 API Integration

### Required API Endpoints
The application expects your Azure DevOps API to provide:

#### GET /api/EnvironmentReport
**Parameters:**
- `environmentName` (optional): Filter by environment name
- `result` (optional): Filter by deployment result
- `pageNumber` (optional): Page number (default: 1)
- `pageSize` (optional): Items per page (default: 10)
- `includeVariableGroups` (optional): Include variable groups (default: true)

**Response Format:**
```json
{
  "data": [
    {
      "environmentId": 1,
      "environmentName": "Production",
      "deploymentRecordResult": "succeeded",
      "deploymentRecordFinishTime": "2024-01-15T10:30:00Z",
      "buildNumber": "20240115.1",
      "variableGroupVariables": {
        "ApiUrl": "https://api.example.com",
        "SecretKey": "[HIDDEN]"
      }
      // ... other fields
    }
  ],
  "totalCount": 25,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

## 🚨 Troubleshooting

### Common Issues

#### CORS Errors
**Problem**: "Access to fetch at '...' has been blocked by CORS policy"
**Solution**: 
- Serve the files through a web server (not file://)
- Configure your API to allow CORS requests
- Add CORS headers to your API responses

#### API Connection Failed
**Problem**: "Unable to connect to the API"
**Solutions**:
1. Verify API URL is correct
2. Ensure API is running and accessible
3. Check network connectivity
4. Verify API endpoints match expected format

#### No Data Displayed
**Problem**: Data loads but no cards appear
**Solutions**:
1. Check browser console for JavaScript errors
2. Verify API response format matches expected structure
3. Check filter settings (especially result filter)
4. Test with `includeVariableGroups=false` for faster loading

#### Performance Issues
**Problem**: Slow loading or unresponsive interface
**Solutions**:
1. Reduce page size in filters
2. Disable variable groups if not needed
3. Enable caching in configuration
4. Check API response times

### Debug Mode
Enable debug mode in `js/config.js`:
```javascript
development: {
    enableDebugMode: true,
    logLevel: 'debug'
}
```

This will log detailed information to the browser console.

## 🔐 Security Considerations

### API Security
- Ensure your API uses HTTPS in production
- Implement proper authentication/authorization
- Never expose sensitive credentials in client-side code
- Use environment variables for configuration

### Content Security Policy
Consider adding CSP headers for enhanced security:
```html
<meta http-equiv="Content-Security-Policy" 
      content="default-src 'self'; 
               script-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com https://cdnjs.cloudflare.com; 
               style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com;">
```

## 📱 Browser Support

### Supported Browsers
- Chrome 60+
- Firefox 55+
- Safari 12+
- Edge 79+

### Required Features
- ES6 (ES2015) support
- Fetch API
- CSS Grid and Flexbox
- Local Storage

## 🤝 Contributing

### Adding New Features
1. Update configuration in `js/config.js`
2. Add API methods in `js/api.js`
3. Implement UI components in `js/ui.js`
4. Wire up functionality in `js/app.js`

### Code Style
- Use consistent indentation (2 spaces)
- Follow existing naming conventions
- Add comments for complex logic
- Test across supported browsers

## 📄 License

This project is provided as-is for use with Azure DevOps Environment Reporter API.

## 🆘 Support

For issues and questions:
1. Check the troubleshooting section above
2. Review browser console for error messages
3. Verify API connectivity and response format
4. Check that all required files are properly deployed