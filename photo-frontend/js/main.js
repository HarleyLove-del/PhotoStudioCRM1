// ИСПРАВЛЕНО: используем HTTPS порт 7042
const API_URL = 'https://localhost:7042/api';

let currentToken = null;
let currentUser = null;

function checkAuth() {
    const token = localStorage.getItem('token');
    const user = localStorage.getItem('user');
    if (!token || !user) {
        window.location.href = '/pages/login.html';
        return false;
    }
    currentToken = token;
    currentUser = JSON.parse(user);
    return true;
}

function logout() {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    window.location.href = '/pages/login.html';
}

async function apiRequest(endpoint, options = {}) {
    try {
        console.log(`Запрос: ${API_URL}${endpoint}`);
        const response = await fetch(`${API_URL}${endpoint}`, {
            ...options,
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${currentToken}`,
                ...options.headers
            }
        });
        
        if (response.status === 401) {
            logout();
            throw new Error('Сессия истекла');
        }
        return response;
    } catch (error) {
        console.error('API Error:', error);
        throw error;
    }
}

function getStatusName(status) {
    const statuses = { 0: 'Ожидает', 1: 'Подтвержден', 2: 'В работе', 3: 'Выполнен', 4: 'Отменен' };
    return statuses[status] || 'Неизвестно';
}

function getStatusClass(status) {
    const classes = { 0: 'status-pending', 2: 'status-progress', 3: 'status-completed' };
    return classes[status] || 'status-pending';
}

function renderOrdersTable(orders) {
    if (!orders || orders.length === 0) return '<p>Нет заказов</p>';
    let html = '<table><thead><tr><th>ID</th><th>Дата</th><th>Сумма</th><th>Статус</th></tr></thead><tbody>';
    orders.forEach(order => {
        html += `<tr>
            <td>${order.id}</td>
            <td>${new Date(order.orderDate).toLocaleDateString()}</td>
            <td>${order.totalAmount} ₽</td>
            <td><span class="status ${getStatusClass(order.status)}">${getStatusName(order.status)}</span></td>
        </tr>`;
    });
    html += '</tbody></table>';
    return html;
}

function renderServices(services) {
    if (!services || services.length === 0) return '<p>Нет услуг</p>';
    return services.map(service => `
        <div class="card">
            <h3>${service.name}</h3>
            <p>${service.description || 'Нет описания'}</p>
            <p><strong>💰 Цена:</strong> ${service.price} ₽</p>
            <p><strong>⏱ Длительность:</strong> ${service.durationMinutes} мин</p>
        </div>
    `).join('');
}

function showAlert(message, type = 'success') {
    const alertDiv = document.createElement('div');
    alertDiv.className = `alert alert-${type}`;
    alertDiv.textContent = message;
    alertDiv.style.position = 'fixed';
    alertDiv.style.top = '20px';
    alertDiv.style.right = '20px';
    alertDiv.style.zIndex = '9999';
    alertDiv.style.background = type === 'success' ? '#d4edda' : '#f8d7da';
    alertDiv.style.color = type === 'success' ? '#155724' : '#721c24';
    alertDiv.style.padding = '15px';
    alertDiv.style.borderRadius = '10px';
    document.body.appendChild(alertDiv);
    setTimeout(() => alertDiv.remove(), 3000);
}