const statusPill = document.getElementById('status-pill');
const banner = document.getElementById('banner');
let csrfToken = '';

async function loadCsrf() {
  const response = await fetch('/api/csrf', { credentials: 'same-origin' });
  if (response.status === 401) {
    window.location.href = '/login.html';
    throw new Error('auth');
  }
  const payload = await response.json().catch(() => ({}));
  csrfToken = payload.token || '';
}

async function api(path, options) {
  options = options || {};
  if (!csrfToken && (options.method || 'GET').toUpperCase() !== 'GET') {
    await loadCsrf();
  }
  const headers = Object.assign({}, options.headers || {});
  if (csrfToken) {
    headers['X-CSRF-TOKEN'] = csrfToken;
  }
  const response = await fetch(path, Object.assign({ credentials: 'same-origin' }, options, { headers }));
  if (response.status === 401) {
    window.location.href = '/login.html';
    throw new Error('auth');
  }
  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw new Error(payload.error || 'Request failed');
  }
  return payload;
}

function showBanner(text, isError) {
  banner.hidden = !text;
  banner.textContent = text || '';
  banner.style.background = isError ? '#3a1c1c' : '#3a3114';
  banner.style.color = isError ? '#e85d5d' : '#f5c542';
}

function renderTextList(element, items, emptyText, toText) {
  element.replaceChildren();
  if (!items || items.length === 0) {
    const li = document.createElement('li');
    li.textContent = emptyText;
    element.appendChild(li);
    return;
  }
  for (const item of items) {
    const li = document.createElement('li');
    li.textContent = toText(item);
    element.appendChild(li);
  }
}

async function refresh() {
  try {
    const meResponse = await fetch('/api/me', { credentials: 'same-origin' });
    if (meResponse.status === 401) {
      window.location.href = '/login.html';
      return;
    }
    const me = await meResponse.json();
    if (!me.authenticated) {
      window.location.href = '/login.html';
      return;
    }
    await loadCsrf();
    const status = await api('/api/status');
    document.getElementById('server-name').textContent = status.server || 'Dedicated server';
    statusPill.textContent = status.status || 'OFFLINE';
    statusPill.className = 'pill ' + String(status.status || 'offline').toLowerCase();
    document.getElementById('players').textContent = `${status.players} / ${status.maxPlayers}`;
    document.getElementById('uptime').textContent = status.uptime || '—';
    document.getElementById('updates').textContent = `${status.modsNeedingUpdate || 0} mods`;
    if (status.crashLoop) {
      showBanner('SERVER CRASH LOOP DETECTED. Automatic restart is paused.', true);
    } else if (status.lastError) {
      showBanner(status.lastError, true);
    } else if (status.busy) {
      showBanner(status.currentAction || 'Working...', false);
    } else {
      showBanner('', false);
    }

    const activity = await api('/api/activity');
    const list = document.getElementById('activity');
    const items = (activity || []).slice().reverse().slice(0, 12);
    renderTextList(list, items, 'No activity yet.', item => {
      const time = new Date(item.timestamp).toLocaleTimeString();
      return `${time} ${item.message}`;
    });
  } catch (err) {
    if (err.message !== 'auth') {
      showBanner(err.message, true);
    }
  }
}

document.querySelectorAll('button[data-action]').forEach(button => {
  button.addEventListener('click', async () => {
    button.disabled = true;
    try {
      await api(button.dataset.action, { method: 'POST' });
      await refresh();
    } catch (err) {
      showBanner(err.message, true);
    } finally {
      button.disabled = false;
    }
  });
});

document.getElementById('delayed').addEventListener('click', async () => {
  const minutes = window.prompt('Restart in how many minutes? (5, 10, 15, 30, 60)', '10');
  if (!minutes) {
    return;
  }
  try {
    await api('/api/server/delayed-restart', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ minutes: Number(minutes) })
    });
    await refresh();
  } catch (err) {
    showBanner(err.message, true);
  }
});

document.getElementById('view-players').addEventListener('click', async () => {
  const panel = document.getElementById('players-panel');
  panel.hidden = !panel.hidden;
  if (panel.hidden) {
    return;
  }
  const players = await api('/api/players');
  renderTextList(
    document.getElementById('player-list'),
    players,
    'No players online (or RCON is not connected).',
    p => p.name);
});

document.getElementById('view-logs').addEventListener('click', async () => {
  const panel = document.getElementById('logs-panel');
  panel.hidden = !panel.hidden;
  if (panel.hidden) {
    return;
  }
  const logs = await api('/api/logs');
  renderTextList(
    document.getElementById('log-list'),
    (logs || []).slice(-40).reverse(),
    'No log lines yet.',
    l => `${l.level}: ${l.message}`);
});

document.getElementById('logout').addEventListener('click', async () => {
  try {
    await api('/api/logout', { method: 'POST' });
  } catch (err) {
    if (err.message !== 'auth') {
      showBanner(err.message, true);
      return;
    }
  }
  window.location.href = '/login.html';
});

refresh();
setInterval(refresh, 5000);
