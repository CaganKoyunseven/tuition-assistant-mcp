// Chat UI for the University Tuition Assistant. No framework, no build step.
// Security: all model/user/API text is inserted as plain text nodes (textContent), never parsed as markup.
// The JWT lives only in this variable (lost on refresh by design); it is never stored.
(() => {
  'use strict';

  const API = '/api/v1';
  const EXAMPLES = [
    ['When is the payment deadline for the fall term?', 'MCP only'],
    ['What is my balance?', 'REST only'],
    ['Can I pay my outstanding balance in installments?', 'REST + MCP'],
    ['Pay $100 of my 2026-FALL balance', 'Payment'],
    ['What is the balance of student 20230002?', 'Privacy test'],
  ];

  let token = null;
  let studentNo = '';
  let sessionId = crypto.randomUUID();
  let busy = false;

  const $ = (id) => document.getElementById(id);
  const el = (tag, className, text) => {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined) node.textContent = text;
    return node;
  };

  // ---------- API helper ----------
  async function api(path, options = {}) {
    const response = await fetch(API + path, {
      method: options.method || 'GET',
      headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) },
      body: options.body ? JSON.stringify(options.body) : undefined,
    });
    let data = null;
    try { data = await response.json(); } catch { /* empty body */ }
    if (response.status === 401 && token) { logout('Your session expired. Please sign in again.'); }
    if (!response.ok) {
      const message = (data && (data.detail || data.title)) || `Request failed (HTTP ${response.status}).`;
      const error = new Error(message);
      error.status = response.status;
      error.code = data && data.code;
      throw error;
    }
    return data;
  }

  // ---------- login / logout ----------
  $('loginForm').addEventListener('submit', async (event) => {
    event.preventDefault();
    const error = $('loginError');
    error.hidden = true;
    try {
      const data = await api('/auth/token', {
        method: 'POST',
        body: { grantType: 'password', username: $('username').value.trim(), password: $('password').value },
      });
      token = data.accessToken;
      studentNo = decodeStudentNo(token) || $('username').value.trim();
      $('password').value = '';
      await showChat();
    } catch (e) {
      error.textContent = e.status === 401 ? 'Wrong student number or password.' : e.message;
      error.hidden = false;
    }
  });

  function decodeStudentNo(jwt) {
    try {
      const payload = JSON.parse(atob(jwt.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
      return payload.student_no || '';
    } catch { return ''; }
  }

  function logout(message) {
    token = null; studentNo = '';
    sessionId = crypto.randomUUID();
    $('messages').replaceChildren();
    $('topRight').hidden = true;
    $('chatView').hidden = true;
    $('loginView').hidden = false;
    if (message) { $('loginError').textContent = message; $('loginError').hidden = false; }
  }
  $('logout').addEventListener('click', () => logout());
  $('newChat').addEventListener('click', () => { sessionId = crypto.randomUUID(); $('messages').replaceChildren(); greet(); });

  // ---------- chat screen ----------
  async function showChat() {
    $('loginView').hidden = true;
    $('chatView').hidden = false;
    $('topRight').hidden = false;
    $('who').textContent = 'Student ' + studentNo;
    await loadModels();
    renderExamples();
    sessionId = crypto.randomUUID();
    $('messages').replaceChildren();
    greet();
    $('messageInput').focus();
  }

  async function loadModels() {
    const select = $('modelSelect');
    select.replaceChildren();
    try {
      const data = await api('/agent/models');
      for (const model of data.models) {
        const option = el('option', '', model + (model === data.defaultModel ? ' (default)' : ''));
        option.value = model;
        select.appendChild(option);
      }
      select.value = data.defaultModel;
      select.disabled = data.models.length < 2;
      select.title = data.models.length < 2 ? 'Only one model is configured (LLM_ALLOWED_MODELS adds more).' : 'Choose the model for the next question';
    } catch { select.disabled = true; }
  }

  function renderExamples() {
    const box = $('examples');
    box.replaceChildren();
    for (const [text, label] of EXAMPLES) {
      const button = el('button', '', `${label}: ${text}`);
      button.type = 'button';
      button.addEventListener('click', () => send(text));
      box.appendChild(button);
    }
  }

  function greet() {
    addMessage('system', 'Hello! Ask about deadlines, installments, late fees, your balance, or pay part of your tuition. Payments always need your Confirm click.');
  }

  function addMessage(role, text) {
    const wrap = el('div');
    if (role === 'user' || role === 'assistant') wrap.appendChild(el('div', 'role ' + role, role === 'user' ? 'Student' : 'AI assistant response'));
    const bubble = el('div', 'bubble ' + role);
    appendFormatted(bubble, text);
    wrap.appendChild(bubble);
    $('messages').appendChild(wrap);
    scrollDown();
    return wrap;
  }

  // Minimal safe formatting: **bold** only, everything else is plain text.
  function appendFormatted(parent, text) {
    String(text).split(/(\*\*[^*]+\*\*)/g).forEach((part) => {
      if (part.startsWith('**') && part.endsWith('**') && part.length > 4) parent.appendChild(el('strong', '', part.slice(2, -2)));
      else parent.appendChild(document.createTextNode(part));
    });
  }

  function scrollDown() { const m = $('messages'); m.scrollTop = m.scrollHeight; }

  $('chatForm').addEventListener('submit', (event) => {
    event.preventDefault();
    const input = $('messageInput');
    const text = input.value.trim();
    if (text) { input.value = ''; send(text); }
  });

  async function send(text) {
    if (busy) return;
    setBusy(true);
    addMessage('user', text);
    const waiting = addMessage('system', 'Thinking…');
    waiting.firstChild.classList.add('thinking');
    try {
      const model = $('modelSelect').disabled ? undefined : $('modelSelect').value;
      const data = await api('/agent/chat', { method: 'POST', body: { sessionId, message: text, model } });
      waiting.remove();
      const message = addMessage('assistant', data.answer);
      message.appendChild(renderTrace(data.trace, data.model));
      if (data.pendingPayment) message.appendChild(renderPendingCard(data.pendingPayment));
    } catch (e) {
      waiting.remove();
      addMessage('problem', e.message);
    } finally { setBusy(false); }
  }

  function setBusy(value) {
    busy = value;
    $('sendBtn').disabled = value;
    $('messageInput').disabled = value;
    if (!value) $('messageInput').focus();
  }

  // ---------- trace panel ----------
  const KIND_LABEL = { llm: 'LLM', rest: 'REST via Gateway', mcp: 'MCP', local: 'Agent (local)', confirmation: 'User confirmation' };

  function renderTrace(trace, model) {
    const details = el('details', 'trace');
    details.open = true;
    details.appendChild(el('summary', '', 'Agent tool execution'));
    const steps = el('div', 'steps');
    for (const step of trace.steps) {
      for (const row of describeStep(step)) steps.appendChild(row);
    }
    details.appendChild(steps);
    const t = trace.totals;
    details.appendChild(el('div', 'totals',
      `Model ${model || trace.model} · ${t.inputTokens} input + ${t.outputTokens} output tokens · estimated cost $${Number(t.estimatedCostUsd).toFixed(6)} · outcome ${trace.outcome}`));
    const raw = el('details', 'raw');
    raw.appendChild(el('summary', '', 'Raw trace (JSON)'));
    raw.appendChild(el('pre', '', JSON.stringify(trace, null, 2)));
    details.appendChild(raw);
    return details;
  }

  function row(kind, detail, ms, isError) {
    const r = el('div', 'step' + (isError ? ' err' : ''));
    r.appendChild(el('div', 'kind', kind));
    const d = el('div', 'detail', detail);
    if (ms !== undefined && ms !== null) d.appendChild(el('span', 'ms', `  (${ms} ms)`));
    r.appendChild(d);
    return r;
  }

  function describeStep(step) {
    const failed = step.status === 'ERROR';
    const out = step.output || {};
    switch (step.kind) {
      case 'llm': {
        const tokens = step.usage ? ` [${step.usage.input} in / ${step.usage.output} out tokens]` : '';
        const text = step.name === 'tool_selection'
          ? 'Chooses ' + (out.toolCalls || []).join(' + ') + tokens
          : 'Writes the final answer' + tokens;
        return [row(KIND_LABEL.llm, text, step.durationMs)];
      }
      case 'rest':
        return [row(KIND_LABEL.rest, `${step.request || step.name} → HTTP ${step.httpStatus ?? '?'}${summarizeRest(step)}`, step.durationMs, failed)];
      case 'mcp': {
        const rows = [row(KIND_LABEL.mcp, `${step.name}(${formatInput(step.input)})`, step.durationMs, failed)];
        rows.push(row('MCP Result', summarizeMcp(out), null, failed));
        return rows;
      }
      case 'confirmation':
        return [row(KIND_LABEL.confirmation, 'Student pressed Confirm' + (out.confirmedAtUtc ? ' at ' + out.confirmedAtUtc : ''), step.durationMs)];
      default:
        return [row(KIND_LABEL.local, `${step.name}(${formatInput(step.input)}) → ${summarizeLocal(out)}`, step.durationMs, failed)];
    }
  }

  function formatInput(input) {
    if (!input || typeof input !== 'object') return '';
    return Object.entries(input).map(([k, v]) => `${k}: ${JSON.stringify(v)}`).join(', ');
  }
  function money(v) { return typeof v === 'number' ? '$' + v.toFixed(2) : String(v); }
  function summarizeRest(step) {
    const o = step.output || {};
    if (o.error) return ' · ' + o.error.message;
    if (step.name === 'pay_tuition') return o.remainingBalance !== undefined ? ` · remaining balance ${money(o.remainingBalance)}` : (o.detail ? ' · ' + o.detail : '');
    if (o.balance !== undefined) return ` · balance ${money(o.balance)} (total ${money(o.tuitionTotal)}, paid ${money(o.paidAmount)})`;
    return '';
  }
  function summarizeMcp(o) {
    if (o.error) return o.error.message;
    if (Array.isArray(o.matches)) {
      return o.matches.length
        ? o.matches.map((m) => `§${m.section} ${m.heading}`).join('; ')
        : (o.message || 'No relevant sections');
    }
    return 'Result truncated in trace; see raw JSON';
  }
  function summarizeLocal(o) {
    if (o.error) return o.error.message;
    if (o.status === 'AWAITING_USER_CONFIRMATION') return `payment of ${money(o.amount)} for ${o.term} prepared, awaiting your confirmation`;
    return 'ok';
  }

  // ---------- pending payment card ----------
  function renderPendingCard(pending) {
    const card = el('div', 'pay');
    card.appendChild(el('h3', '', 'Payment awaiting your confirmation'));
    const list = el('dl');
    const add = (k, v) => { list.appendChild(el('dt', '', k)); list.appendChild(el('dd', '', v)); };
    add('Student', pending.studentNo);
    add('Term', pending.term);
    add('Amount', money(pending.amount) + ' ' + pending.currency);
    add('Current balance', money(pending.currentBalance));
    add('Remaining after payment', money(pending.remainingAfterPayment));
    card.appendChild(list);

    const actions = el('div', 'actions');
    const confirm = el('button', 'primary', 'Confirm');
    const cancel = el('button', '', 'Cancel');
    const timer = el('span', 'timer');
    [confirm, cancel].forEach((b) => { b.type = 'button'; });
    actions.append(confirm, cancel, timer);
    card.appendChild(actions);
    const result = el('div', 'result');
    card.appendChild(result);

    const expires = new Date(pending.expiresAt).getTime();
    const tick = setInterval(() => {
      const left = Math.max(0, Math.round((expires - Date.now()) / 1000));
      timer.textContent = left > 0 ? `expires in ${Math.floor(left / 60)}:${String(left % 60).padStart(2, '0')}` : 'expired';
      if (left === 0) { confirm.disabled = true; clearInterval(tick); }
    }, 1000);
    timer.textContent = 'expires in 5:00';

    const finish = (cls, text) => {
      clearInterval(tick);
      confirm.disabled = cancel.disabled = true;
      timer.textContent = '';
      card.classList.add(cls);
      result.textContent = text;
    };

    confirm.addEventListener('click', async () => {
      confirm.disabled = cancel.disabled = true;
      try {
        const data = await api(`/agent/payments/${pending.pendingId}/confirm`, { method: 'POST', body: { sessionId } });
        finish(data.status === 'SUCCESS' ? 'done' : 'failed', data.answer);
        card.parentElement.appendChild(renderTrace(data.trace, 'n/a'));
        scrollDown();
      } catch (e) { finish('failed', e.message); }
    });
    cancel.addEventListener('click', async () => {
      confirm.disabled = cancel.disabled = true;
      try {
        await api(`/agent/payments/${pending.pendingId}/cancel`, { method: 'POST', body: { sessionId } });
        finish('failed', 'Payment cancelled. Nothing was charged.');
      } catch (e) { finish('failed', e.message); }
    });
    return card;
  }
})();
