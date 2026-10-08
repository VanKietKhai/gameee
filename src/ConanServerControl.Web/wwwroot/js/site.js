const $ = id => document.getElementById(id);
let csrfToken = '';
let last = null;

const STATUS = {
  Online: ['ĐANG CHẠY', 'online'],
  Offline: ['ĐÃ TẮT', 'offline'],
  Starting: ['ĐANG KHỞI ĐỘNG', 'starting'],
  Stopping: ['ĐANG TẮT', 'starting'],
  Restarting: ['ĐANG KHỞI ĐỘNG LẠI', 'starting'],
  Updating: ['ĐANG CẬP NHẬT', 'starting'],
  Error: ['LỖI', 'error'],
  Unresponsive: ['KHÔNG PHẢN HỒI', 'error']
};

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
  const method = (options.method || 'GET').toUpperCase();
  if (!csrfToken && method !== 'GET') {
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
    throw new Error(payload.error || 'Có lỗi xảy ra.');
  }
  return payload;
}

function post(path, body) {
  return api(path, {
    method: 'POST',
    headers: body ? { 'Content-Type': 'application/json' } : {},
    body: body ? JSON.stringify(body) : undefined
  });
}

function showBanner(text, isError) {
  const banner = $('banner');
  banner.hidden = !text;
  banner.textContent = text || '';
  banner.className = 'banner' + (isError ? ' bad' : '');
}

function fillList(element, items, emptyText, toText) {
  element.replaceChildren();
  const rows = items && items.length ? items : [null];
  for (const item of rows) {
    const li = document.createElement('li');
    li.textContent = item === null ? emptyText : toText(item);
    element.appendChild(li);
  }
}

function render(s) {
  last = s;
  const [label, css] = STATUS[s.status] || [String(s.status || '').toUpperCase(), 'offline'];
  $('status-pill').textContent = label;
  $('status-pill').className = 'pill ' + css;
  $('server-name').textContent = s.server || 'Conan Server';
  $('uptime').textContent = s.uptime ? 'Đã chạy ' + s.uptime : ' ';
  $('join-address').textContent = s.joinAddress || '—';
  $('player-count').textContent = `${s.players} / ${s.maxPlayers}`;
  fillList($('player-list'), s.playerNames, 'Chưa có ai online.', n => n);

  const online = s.status === 'Online' || s.status === 'Unresponsive';
  const busy = s.busy || ['Starting', 'Stopping', 'Restarting', 'Updating'].includes(s.status);
  $('start').hidden = online || s.status === 'Starting';
  $('stop').hidden = !online;
  $('restart').hidden = !online;
  for (const id of ['start', 'stop', 'restart']) {
    $(id).disabled = busy;
  }

  $('countdown').hidden = s.countdownMinutes == null;
  if (s.countdownMinutes != null) {
    $('countdown-text').textContent =
      `Server sẽ ${s.countdownStopOnly ? 'tắt' : 'khởi động lại'} sau ${s.countdownMinutes} phút`;
  }

  let build = s.installedBuild ? 'Build server ' + s.installedBuild : '';
  if (s.serverUpdateAvailable && s.availableBuild) {
    build += ` - có bản mới ${s.availableBuild}`;
  }
  if (s.modsNeedingUpdate) {
    build += ` - ${s.modsNeedingUpdate} mod có bản mới`;
  }
  $('build').textContent = build || ' ';

  if (s.crashLoop) {
    showBanner('Server crash liên tục, đã ngừng tự khởi động lại.', true);
  } else if (s.lastError) {
    showBanner(s.lastError, true);
  } else if (s.busy && s.currentAction) {
    showBanner('Đang xử lý: ' + s.currentAction, false);
  } else {
    showBanner('', false);
  }
}

async function refresh() {
  try {
    render(await api('/api/status'));
    const activity = await api('/api/activity');
    const items = (activity || []).slice().reverse().slice(0, 8); // API returns oldest first
    fillList($('activity'), items, 'Chưa có hoạt động.', item => {
      const time = new Date(item.timestamp).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
      return `${time}  ${item.message}`;
    });
  } catch (err) {
    if (err.message !== 'auth') {
      showBanner('Không kết nối được tới máy chủ. Kiểm tra Tailscale đang bật.', true);
    }
  }
}

async function act(fn) {
  try {
    await fn();
  } catch (err) {
    showBanner(err.message, true);
  }
  setTimeout(refresh, 500);
}

function askStopOrRestart(action) {
  const verb = action === 'stop' ? 'tắt' : 'khởi động lại';
  const players = last ? last.players : 0;
  if (!players) {
    if (window.confirm(`Không có ai online. ${verb.charAt(0).toUpperCase() + verb.slice(1)} server ngay?`)) {
      act(() => post('/api/server/' + action));
    }
    return;
  }
  $('sheet-text').textContent = `Đang có ${players} người chơi online. Bạn muốn ${verb} server thế nào?`;
  $('sheet').hidden = false;
  $('sheet-later').onclick = () => { $('sheet').hidden = true; act(() => post('/api/server/scheduled', { action })); };
  $('sheet-now').onclick = () => { $('sheet').hidden = true; act(() => post('/api/server/' + action)); };
}

function copyText(text) {
  // navigator.clipboard needs HTTPS; over plain http (Tailscale address) fall back to a selection copy.
  if (navigator.clipboard && window.isSecureContext) {
    return navigator.clipboard.writeText(text);
  }
  const area = document.createElement('textarea');
  area.value = text;
  area.setAttribute('readonly', '');
  area.style.position = 'fixed';
  area.style.opacity = '0';
  document.body.appendChild(area);
  area.select();
  area.setSelectionRange(0, text.length);
  const ok = document.execCommand('copy');
  document.body.removeChild(area);
  return ok ? Promise.resolve() : Promise.reject(new Error('copy'));
}

$('start').addEventListener('click', () => act(() => post('/api/server/start')));
$('stop').addEventListener('click', () => askStopOrRestart('stop'));
$('restart').addEventListener('click', () => askStopOrRestart('restart'));
$('sheet-cancel').addEventListener('click', () => { $('sheet').hidden = true; });
$('cancel-countdown').addEventListener('click', () => act(() => post('/api/server/cancel-restart')));
$('copy').addEventListener('click', () => {
  const text = $('join-address').textContent;
  copyText(text)
    .then(() => { $('copy-feedback').textContent = 'Đã copy ' + text; })
    .catch(() => { $('copy-feedback').textContent = 'Không copy được, hãy giữ tay vào địa chỉ để copy.'; });
  setTimeout(() => { $('copy-feedback').textContent = ' '; }, 3000);
});
$('logout').addEventListener('click', async () => {
  try {
    await post('/api/logout');
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
document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh(); });
