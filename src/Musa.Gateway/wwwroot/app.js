// Musa web. No framework and no build step: the gateway serves these files as they are.
const api = (path, options) => fetch(`/api/${path}`, options);
const $ = (selector) => document.querySelector(selector);
const money = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });
const day = new Intl.DateTimeFormat('es-MX', { day: 'numeric', month: 'long', year: 'numeric' });
const esc = (text) => String(text ?? '').replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`);

const state = { pieces: [], lots: new Map(), discipline: '', paddle: localStorage.getItem('musa.paddle') ?? 'P-101' };

function toast(message) {
  const el = $('#toast');
  el.textContent = message;
  el.classList.add('show');
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => el.classList.remove('show'), 4200);
}

async function send(path, body) {
  const response = await api(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  if (response.ok) return response.json();
  if (response.status === 429) throw new Error('Demasiadas pujas seguidas. Respira y vuelve a intentarlo.');
  const problem = await response.json().catch(() => ({}));
  throw new Error(problem.title ?? `La casa no pudo atender la petición (${response.status}).`);
}

// ── The sale room ───────────────────────────────────────────────────────────
function lotCard(lot) {
  const open = lot.status === 'Open';
  const mine = lot.leadingPaddle === state.paddle;
  const outcome = lot.status === 'Sold'
    ? `Rematado en <b>${money.format(lot.currentBid)}</b> · paleta ${esc(lot.leadingPaddle)}`
    : 'Sin remate: no se alcanzó la reserva.';
  const standing = !lot.currentBid ? `Salida en <b>${money.format(lot.startingBid)}</b>`
    : mine ? `<span class="leading">Vas en cabeza</span> · ${lot.reserveMet ? 'reserva alcanzada' : 'reserva aún no alcanzada'}`
    : `Lidera la paleta <b>${esc(lot.leadingPaddle)}</b> · ${lot.reserveMet ? 'reserva alcanzada' : 'reserva aún no alcanzada'}`;
  return `
    <article class="lot ${open ? '' : 'closed'}" data-lot="${lot.lotNumber}">
      <img src="/api/catalog/pieces/${lot.pieceId}/artwork.svg" alt="" loading="lazy">
      <div>
        <p class="meta">Lote ${lot.lotNumber} · ${lot.bids.length} ${lot.bids.length === 1 ? 'puja' : 'pujas'}</p>
        <h3>${esc(lot.title)}</h3>
        <p class="meta">${esc(lot.creator)}<br>Est. ${money.format(lot.estimateLow)} – ${money.format(lot.estimateHigh)}</p>
      </div>
      <div class="figures">
        <span class="price">${lot.currentBid ? money.format(lot.currentBid) : '—'}</span>
        <span class="clock" data-closes="${lot.closesAt}">${open ? '' : lot.status === 'Sold' ? 'Vendido' : 'Retirado'}</span>
      </div>
      <p class="state">${open ? standing : outcome}</p>
      ${open ? `<button data-bid="${lot.lotNumber}" data-amount="${lot.nextMinimumBid}" ${mine ? 'disabled' : ''}>Pujar ${money.format(lot.nextMinimumBid)}</button>` : ''}
    </article>`;
}

function renderLots(flash) {
  const lots = [...state.lots.values()].sort((a, b) =>
    (a.status === 'Open' ? 0 : 1) - (b.status === 'Open' ? 0 : 1) || new Date(a.closesAt) - new Date(b.closesAt));
  $('#lots').innerHTML = lots.map(lotCard).join('');
  const live = lots.filter((lot) => lot.status === 'Open').length;
  $('#room-status').textContent = live ? `${live} ${live === 1 ? 'lote' : 'lotes'} en la tarima. Las pujas llegan en directo.` : 'La sala está en silencio.';
  if (flash) $(`[data-lot="${flash}"]`)?.classList.add('flash');
  tick();
}

function tick() {
  for (const clock of document.querySelectorAll('.clock[data-closes]')) {
    const lot = state.lots.get(Number(clock.closest('[data-lot]').dataset.lot));
    if (lot?.status !== 'Open') continue;
    const left = Math.max(0, new Date(clock.dataset.closes) - Date.now());
    const seconds = Math.ceil(left / 1000);
    clock.textContent = left === 0 ? 'Cae el martillo…' : `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`;
    clock.classList.toggle('closing', left < 20_000);
  }
}

function watchRoom() {
  const stream = new EventSource('/api/auctions/live'); // reconnects on its own if the connection drops
  stream.addEventListener('lot', (message) => {
    const lot = JSON.parse(message.data);
    const before = state.lots.get(lot.lotNumber);
    state.lots.set(lot.lotNumber, lot);
    renderLots(lot.lotNumber);
    if (before?.status !== lot.status || !before) {
      // The other services hear about this through the event bus a moment later; give them that moment.
      setTimeout(() => { loadPieces(); loadJournal(); }, 1800);
    }
  });
}

// ── The collection ──────────────────────────────────────────────────────────
const disciplines = { Art: 'Arte', Fashion: 'Moda', Design: 'Diseño' };

function pieceCard(piece) {
  const sold = piece.availability === 'Sold';
  const terms = piece.price
    ? `<span>${money.format(piece.price)}</span>`
    : `<span>Est. ${money.format(piece.estimateLow)} – ${money.format(piece.estimateHigh)}</span>`;
  const action = sold ? '<span class="tag">Vendida</span>'
    : piece.availability === 'OnTheBlock' ? '<a class="tag" href="#sala">En la sala</a>'
    : piece.price ? `<button data-buy="${piece.id}">Adquirir</button>`
    : `<button class="quiet" data-consign="${piece.id}">Llevar a subasta</button>`;
  return `
    <article class="piece ${sold ? 'sold' : ''}">
      <button class="frame" data-open="${piece.id}" aria-label="Ver ${esc(piece.title)} y su procedencia">
        <img src="/api/catalog/${piece.artwork}" alt="Retrato generativo de ${esc(piece.title)}" loading="lazy">
      </button>
      <p class="meta">${disciplines[piece.discipline]} · ${piece.year}</p>
      <h3>${esc(piece.title)}</h3>
      <p class="meta">${esc(piece.creator)}<br>${esc(piece.medium)}</p>
      <div class="terms">${terms}${action}</div>
    </article>`;
}

function renderPieces() {
  $('#pieces').innerHTML = state.pieces.filter((p) => !state.discipline || p.discipline === state.discipline).map(pieceCard).join('');
}

async function loadPieces() {
  state.pieces = await (await api('catalog/pieces')).json();
  renderPieces();
}

async function openSheet(id) {
  const piece = state.pieces.find((p) => p.id === id);
  const provenance = await (await api(`provenance/pieces/${id}`)).json();
  const sheet = $('#sheet');
  sheet.innerHTML = `
    <div class="sheet">
      <img src="/api/catalog/${piece.artwork}" alt="Retrato generativo de ${esc(piece.title)}">
      <div class="text">
        <form method="dialog"><button class="quiet close" aria-label="Cerrar">Cerrar</button></form>
        <p class="meta">${disciplines[piece.discipline]} · ${piece.year}</p>
        <h2 id="sheet-title">${esc(piece.title)}</h2>
        <p class="meta">${esc(piece.creator)} · ${esc(piece.medium)}${piece.dimensions ? ` · ${esc(piece.dimensions)}` : ''}</p>
        <p class="story">${esc(piece.story)}</p>
        <p class="meta">Procedencia · <span class="${provenance.intact ? 'intact' : 'broken'}">${provenance.intact ? 'cadena íntegra' : 'cadena alterada'}</span></p>
        <ol class="chain">${provenance.entries.map((entry) => `
          <li><span class="meta">${day.format(new Date(entry.occurredAt))} · ${esc(entry.kind)}</span><br>
              ${esc(entry.statement)}<br><code title="${entry.hash}">sha-256 ${entry.hash.slice(0, 16)}…</code></li>`).join('')}
        </ol>
      </div>
    </div>`;
  sheet.showModal();
}

// ── The journal ─────────────────────────────────────────────────────────────
const kinds = { Essay: 'Ensayo', Note: 'Nota', Chronicle: 'Crónica de la casa' };

async function loadJournal() {
  const entries = await (await api('journal/entries')).json();
  $('#entries').innerHTML = entries.map((entry) => `
    <article class="entry ${entry.kind}">
      <p class="meta"><span class="tag">${kinds[entry.kind]}</span> · ${day.format(new Date(entry.publishedAt))} · ${esc(entry.author)}</p>
      <h3>${esc(entry.title)}</h3>
      <p>${esc(entry.body)}</p>
    </article>`).join('');
}

// ── Wiring ──────────────────────────────────────────────────────────────────
document.addEventListener('click', async (event) => {
  const target = event.target.closest('button, a');
  if (!target) return;
  const { bid, amount, buy, consign, open, discipline } = target.dataset;
  try {
    if (bid) {
      target.disabled = true;
      await send(`auctions/lots/${bid}/bids`, { paddle: state.paddle, amount: Number(amount) });
    } else if (buy) {
      target.disabled = true;
      const order = await send('ordering/orders', { pieceId: buy, paddle: state.paddle });
      toast(`«${order.title}» es tuya. El registro de procedencia ya lo sabe.`);
      setTimeout(() => { loadPieces(); loadJournal(); }, 1500);
    } else if (consign) {
      target.disabled = true;
      await send('auctions/lots', { pieceId: consign, durationSeconds: 120 });
      $('#sala').scrollIntoView();
    } else if (open) {
      await openSheet(open);
    } else if (discipline !== undefined) {
      state.discipline = discipline;
      document.querySelectorAll('.filters button').forEach((b) => b.setAttribute('aria-pressed', String(b === target)));
      renderPieces();
    }
  } catch (error) {
    toast(error.message);
    target.disabled = false;
  }
});

$('#note').addEventListener('submit', async (event) => {
  event.preventDefault();
  const form = new FormData(event.target);
  try {
    await send('journal/notes', { body: form.get('body'), author: form.get('author') });
    event.target.reset();
    await loadJournal();
  } catch (error) {
    toast(error.message);
  }
});

const paddle = $('#paddle');
paddle.value = state.paddle;
paddle.addEventListener('change', () => {
  state.paddle = paddle.value;
  localStorage.setItem('musa.paddle', state.paddle);
  renderLots();
});

$('#issue').textContent = `N.º 1 — ${day.format(new Date())}`;
for (const lot of await (await api('auctions/lots')).json()) state.lots.set(lot.lotNumber, lot);
renderLots();
watchRoom();
setInterval(tick, 250);
await Promise.all([loadPieces(), loadJournal()]);
