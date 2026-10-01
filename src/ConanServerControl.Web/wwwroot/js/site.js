const statusPill = document.getElementById('status-pill');
const banner = document.getElementById('banner');

async function api(path, options) {
  const response = await fetch(path, Object.assign({ credentials: 'same-origin' }, options));
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

async function refresh() {
  try {
    const me = await fetch('/api/me', { credentials: 'same-origin' }).then(r => r.json());
    if (!me.authenticated) {
      window.location.href = '/login.html';
      return;
    }
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
    list.innerHTML = (activity || []).slice().reverse().slice(0, 12).map(item => {
      const time = new Date(item.timestamp).toLocaleTimeString();
      return `<li>${time} ${item.message}</li>`;
    }).join('') || '<li>No activity yet.</li>';
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
  document.getElementById('player-list').innerHTML =
    (players || []).map(p => `<li>${p.name}</li>`).join('') || '<li>No players online (or RCON is not connected).</li>';
});

document.getElementById('view-logs').addEventListener('click', async () => {
  const panel = document.getElementById('logs-panel');
  panel.hidden = !panel.hidden;
  if (panel.hidden) {
    return;
  }
  const logs = await api('/api/logs');
  document.getElementById('log-list').innerHTML =
    (logs || []).slice(-40).reverse().map(l => `<li>${l.level}: ${l.message}</li>`).join('') || '<li>No log lines yet.</li>';
});

document.getElementById('logout').addEventListener('click', async () => {
  await fetch('/api/logout', { method: 'POST', credentials: 'same-origin' });
  window.location.href = '/login.html';
});

refresh();
setInterval(refresh, 5000);
