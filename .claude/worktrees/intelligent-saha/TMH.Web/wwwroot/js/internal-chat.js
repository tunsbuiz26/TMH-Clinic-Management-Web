/**
 * internal-chat.js
 * Widget chat nội bộ (floating) dùng cho trang Admin & Doctor.
 * Tự inject HTML + CSS vào page, không phụ thuộc framework.
 *
 * Luồng:
 *  1. Hiển thị nút "💬 Nội bộ" ở góc trái dưới (tránh chồng chatbot AI bên phải)
 *  2. Click → mở panel: sidebar danh sách conv + khung chat
 *  3. Kết nối SignalR /hubs/chat (lấy token từ /api/msgs/token)
 *  4. Real-time qua SignalR, fallback REST
 */
(function () {
    'use strict';

    // ── Inject CSS ────────────────────────────────────────────────
    var style = document.createElement('style');
    style.textContent = `
    .ic-bubble {
        position: fixed; bottom: 28px; left: 28px; z-index: 9997;
        width: 52px; height: 52px; border-radius: 50%;
        background: linear-gradient(135deg,#0a4d7c,#1a6fa8);
        color:#fff; border:none; font-size:22px; cursor:pointer;
        display:flex; align-items:center; justify-content:center;
        box-shadow:0 4px 16px rgba(10,77,124,.35);
        transition:transform .15s;
    }
    .ic-bubble:hover { transform:scale(1.08); }
    .ic-badge {
        position:absolute; top:-2px; right:-2px;
        background:#ef4444; color:#fff; font-size:10px; font-weight:700;
        width:18px; height:18px; border-radius:50%;
        display:none; align-items:center; justify-content:center;
        border:2px solid #fff;
    }
    .ic-badge.show { display:flex; }
    .ic-window {
        position:fixed; bottom:90px; left:28px; z-index:9996;
        width:680px; max-width:calc(100vw - 56px); height:500px;
        background:#fff; border-radius:18px;
        box-shadow:0 12px 40px rgba(10,77,124,.18);
        display:none; flex-direction:row; overflow:hidden;
        font-family:'DM Sans',sans-serif;
    }
    .ic-window.open {
        display:flex;
        animation:icFadeUp .2s ease-out;
    }
    @keyframes icFadeUp {
        from { opacity:0; transform:translateY(10px); }
        to   { opacity:1; transform:translateY(0); }
    }
    .ic-sidebar {
        width:230px; border-right:1px solid #e8f0f7;
        display:flex; flex-direction:column; flex-shrink:0;
    }
    .ic-sidebar-hd {
        padding:12px 14px; border-bottom:1px solid #e8f0f7;
        font-size:13px; font-weight:700; color:#0a4d7c;
        display:flex; align-items:center; gap:7px; flex-shrink:0;
    }
    .ic-close {
        margin-left:auto; background:none; border:none;
        color:#aab8c2; font-size:18px; cursor:pointer; line-height:1;
    }
    .ic-close:hover { color:#0a4d7c; }
    .ic-new-btn {
        margin:10px 12px 6px; height:32px; background:#0a4d7c; color:#fff;
        border:none; border-radius:8px; font-size:12px; font-weight:500;
        cursor:pointer; display:flex; align-items:center; justify-content:center;
        gap:5px; font-family:'DM Sans',sans-serif; transition:background .15s;
    }
    .ic-new-btn:hover { background:#1a6fa8; }
    .ic-conv-list { flex:1; overflow-y:auto; }
    .ic-conv-item {
        padding:10px 14px; cursor:pointer; transition:background .12s;
        border-bottom:1px solid #f0f4f8;
    }
    .ic-conv-item:hover { background:#f8fbff; }
    .ic-conv-item.active { background:#e8f4fd; }
    .ic-conv-name {
        font-size:12.5px; font-weight:600; color:#0a4d7c;
        display:flex; align-items:center; justify-content:space-between;
        margin-bottom:2px;
    }
    .ic-conv-preview { font-size:11px; color:#7a9bb0; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .ic-unread-dot { width:7px; height:7px; background:#ef4444; border-radius:50%; flex-shrink:0; }
    .ic-main { flex:1; display:flex; flex-direction:column; }
    .ic-main-hd {
        padding:10px 16px; border-bottom:1px solid #e8f0f7;
        display:flex; align-items:center; gap:10px; background:#f8fbff; flex-shrink:0;
    }
    .ic-main-av {
        width:32px; height:32px; border-radius:50%;
        background:linear-gradient(135deg,#0a4d7c,#1a6fa8);
        color:#fff; font-size:12px; font-weight:700;
        display:flex; align-items:center; justify-content:center; flex-shrink:0;
    }
    .ic-main-name { font-size:13px; font-weight:600; color:#0a4d7c; }
    .ic-main-role { font-size:11px; color:#7a9bb0; }
    .ic-msgs {
        flex:1; overflow-y:auto; padding:12px 14px;
        display:flex; flex-direction:column; gap:7px; background:#f8fbff;
    }
    .ic-msg { display:flex; gap:7px; align-items:flex-end; }
    .ic-msg.mine { flex-direction:row-reverse; }
    .ic-av {
        width:24px; height:24px; border-radius:50%;
        display:flex; align-items:center; justify-content:center;
        font-size:11px; font-weight:700; flex-shrink:0;
    }
    .ic-msg.other .ic-av { background:#e0f2fe; color:#0e7490; }
    .ic-msg.mine  .ic-av { background:#0a4d7c; color:#fff; }
    .ic-msg-body { display:flex; flex-direction:column; min-width:0; max-width:72%; }
    .ic-msg.mine  .ic-msg-body { align-items:flex-end; }
    .ic-msg.other .ic-msg-body { align-items:flex-start; }
    .ic-bub {
        padding:7px 11px; border-radius:12px;
        font-size:12.5px; line-height:1.5; word-break:break-word;
    }
    .ic-msg.other .ic-bub { background:#fff; border:1px solid #dbeafe; color:#0d2d44; border-bottom-left-radius:3px; }
    .ic-msg.mine  .ic-bub { background:#0a4d7c; color:#fff; border-bottom-right-radius:3px; }
    .ic-meta { font-size:10px; color:#94a3b8; margin-top:2px; }
    .ic-msg.mine .ic-meta { text-align:right; }
    .ic-input-row {
        padding:9px 12px; border-top:1px solid #e8f0f7;
        display:flex; gap:7px; background:#fff; flex-shrink:0;
    }
    .ic-input {
        flex:1; border:1.5px solid #d4e8f5; border-radius:12px;
        padding:7px 12px; font-size:12.5px; outline:none;
        font-family:'DM Sans',sans-serif;
    }
    .ic-input:focus { border-color:#0a4d7c; }
    .ic-send {
        width:34px; height:34px; background:#0a4d7c; border:none;
        border-radius:8px; cursor:pointer;
        display:flex; align-items:center; justify-content:center;
        flex-shrink:0; transition:background .15s;
    }
    .ic-send:hover { background:#1a6fa8; }
    .ic-send svg { fill:#fff; width:14px; height:14px; }
    .ic-empty {
        flex:1; display:flex; flex-direction:column; align-items:center;
        justify-content:center; gap:8px; color:#94a3b8; font-size:12.5px;
        padding:20px; text-align:center;
    }
    /* Modal chọn người chat */
    .ic-modal-bg {
        position:fixed; inset:0; background:rgba(0,0,0,.45);
        z-index:10000; display:none; align-items:center; justify-content:center;
    }
    .ic-modal-bg.open { display:flex; }
    .ic-modal {
        background:#fff; border-radius:14px; padding:24px;
        width:360px; max-width:95vw;
        box-shadow:0 12px 40px rgba(0,0,0,.18);
    }
    .ic-modal-title { font-size:15px; font-weight:700; color:#0a4d7c; margin-bottom:16px; }
    .ic-modal select {
        width:100%; height:38px; border:1.5px solid #d4e8f5;
        border-radius:8px; padding:0 12px; font-size:13px;
        font-family:'DM Sans',sans-serif; outline:none; margin-bottom:16px;
    }
    .ic-modal-btns { display:flex; gap:8px; }
    .ic-modal-cancel {
        flex:1; height:38px; background:#f5f5f5; color:#555;
        border:none; border-radius:8px; font-size:13px; cursor:pointer;
        font-family:'DM Sans',sans-serif;
    }
    .ic-modal-ok {
        flex:2; height:38px; background:#0a4d7c; color:#fff;
        border:none; border-radius:8px; font-size:13px; font-weight:600;
        cursor:pointer; font-family:'DM Sans',sans-serif;
    }
    .ic-modal-ok:hover { background:#1a6fa8; }
    `;
    document.head.appendChild(style);

    // ── Inject HTML ───────────────────────────────────────────────
    document.body.insertAdjacentHTML('beforeend', `
    <button class="ic-bubble" id="icBubble" onclick="icToggle()" title="Chat nội bộ">
        💬
        <span class="ic-badge" id="icBadge"></span>
    </button>

    <div class="ic-window" id="icWindow">
        <div class="ic-sidebar">
            <div class="ic-sidebar-hd">
                🏥 Nội bộ
                <button class="ic-close" onclick="icToggle()">✕</button>
            </div>
            <button class="ic-new-btn" onclick="icOpenNewModal()">✏️ Tin nhắn mới</button>
            <div class="ic-conv-list" id="icConvList">
                <div style="padding:14px;text-align:center;font-size:12px;color:#94a3b8;">Đang tải...</div>
            </div>
        </div>
        <div class="ic-main">
            <div class="ic-empty" id="icEmpty">
                <div style="font-size:32px;">💬</div>
                <div>Chọn cuộc hội thoại</div>
            </div>
            <div id="icChatArea" style="display:none;flex:1;flex-direction:column;overflow:hidden;">
                <div class="ic-main-hd" id="icChatHd">
                    <div class="ic-main-av" id="icChatAv">?</div>
                    <div>
                        <div class="ic-main-name" id="icChatName">—</div>
                        <div class="ic-main-role" id="icChatRole">—</div>
                    </div>
                </div>
                <div class="ic-msgs" id="icMsgs"></div>
                <div class="ic-input-row">
                    <input class="ic-input" id="icInput" type="text"
                           placeholder="Nhập tin nhắn..." maxlength="2000"
                           onkeydown="if(event.key==='Enter')icSend()" />
                    <button class="ic-send" onclick="icSend()">
                        <svg viewBox="0 0 24 24"><path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/></svg>
                    </button>
                </div>
            </div>
        </div>
    </div>

    <!-- Modal chọn người -->
    <div class="ic-modal-bg" id="icModalBg" onclick="if(event.target===this)icCloseModal()">
        <div class="ic-modal">
            <div class="ic-modal-title">✏️ Gửi tin nhắn nội bộ</div>
            <select id="icTargetSel">
                <option value="">-- Chọn người nhận --</option>
            </select>
            <div class="ic-modal-btns">
                <button class="ic-modal-cancel" onclick="icCloseModal()">Huỷ</button>
                <button class="ic-modal-ok" onclick="icStartChat()">Bắt đầu</button>
            </div>
        </div>
    </div>
    `);

    // ── State ─────────────────────────────────────────────────────
    var icIsOpen   = false;
    var icConvId   = null;
    var icHubConn  = null;
    var icLoaded   = false;
    var _icSeenIds = new Set(); // dedup ReceiveMessage
    var _icLastMsgId = 0;
    var _icPollTimer = null;

    // Connect hub ngay khi script load để nhận real-time ngay lập tức
    icConnectHub();
    // Polling fallback mỗi 3 giây
    _icPollTimer = setInterval(function() { icPollNewMessages(); }, 3000);

    window.icToggle = function () {
        icIsOpen = !icIsOpen;
        document.getElementById('icWindow').classList.toggle('open', icIsOpen);
        if (icIsOpen) {
            document.getElementById('icBadge').classList.remove('show');
            if (!icLoaded) { icLoaded = true; icLoadConversations(); }
        }
    };

    // ── Load danh sách conv ───────────────────────────────────────
    function icLoadConversations() {
        fetch('/api/msgs/conversations')
        .then(function(r){ return r.json(); })
        .then(function(list) {
            var el = document.getElementById('icConvList');
            el.innerHTML = '';
            if (!list || !list.length) {
                el.innerHTML = '<div style="padding:14px;text-align:center;font-size:12px;color:#94a3b8;">Chưa có tin nhắn nội bộ.</div>';
                return;
            }
            // Chỉ hiển thị Internal conversations
            var internalList = list.filter(function(c){ return c.type === 'Internal'; });
            if (!internalList.length) {
                el.innerHTML = '<div style="padding:14px;text-align:center;font-size:12px;color:#94a3b8;">Chưa có tin nhắn nội bộ.</div>';
            } else {
                internalList.forEach(function(c) { icRenderConvItem(el, c); });
            }
        });
    }

    function icRenderConvItem(container, c) {
        var item = document.createElement('div');
        item.className = 'ic-conv-item';
        item.dataset.convId = c.id;
        var d = new Date(c.lastMessageAt);
        var t = d.getHours().toString().padStart(2,'0') + ':' + d.getMinutes().toString().padStart(2,'0');
        item.innerHTML =
            '<div class="ic-conv-name">' +
            '<span>' + icEsc(c.title) + '</span>' +
            '<div style="display:flex;align-items:center;gap:4px;">' +
            (c.unreadCount > 0 ? '<div class="ic-unread-dot"></div>' : '') +
            '<span style="font-size:10px;color:#aab8c2;">' + t + '</span>' +
            '</div></div>' +
            '<div class="ic-conv-preview">' + icEsc(c.lastMessage || 'Chưa có tin nhắn') + '</div>';
        item.onclick = function() { icOpenConv(c); };
        container.appendChild(item);
    }

    // ── Mở conv ───────────────────────────────────────────────────
    function icOpenConv(conv) {
        icConvId = conv.id;
        document.querySelectorAll('.ic-conv-item').forEach(function(i){ i.classList.remove('active'); });
        var el = document.querySelector('[data-conv-id="' + conv.id + '"]');
        if (el) el.classList.add('active');

        document.getElementById('icChatAv').textContent = conv.title ? conv.title[0].toUpperCase() : '?';
        document.getElementById('icChatName').textContent = conv.title;
        document.getElementById('icChatRole').textContent = 'Nội bộ';

        document.getElementById('icEmpty').style.display = 'none';
        var ca = document.getElementById('icChatArea');
        ca.style.display = 'flex';

        icLoadMessages(conv.id);
        if (icHubConn && icHubConn.state === 'Connected')
            icHubConn.invoke('JoinConversation', conv.id).catch(function(){});

        fetch('/api/msgs/conversations/' + conv.id + '/read', { method: 'PUT' });
    }

    // ── Load messages ─────────────────────────────────────────────
    function icLoadMessages(convId) {
        fetch('/api/msgs/conversations/' + convId + '/messages')
        .then(function(r){ return r.json(); })
        .then(function(msgs) {
            var box = document.getElementById('icMsgs');
            box.innerHTML = '';
            _icSeenIds.clear();
            if (!msgs || !msgs.length) {
                _icLastMsgId = 0;
                box.innerHTML = '<div style="text-align:center;padding:14px;font-size:12px;color:#94a3b8;">Chưa có tin nhắn.</div>';
                return;
            }
            msgs.forEach(function(m){
                if (m.id) _icSeenIds.add(m.id);
                icAppendMsg(m, false);
            });
            var last = msgs[msgs.length - 1];
            _icLastMsgId = last.id || last.Id || 0;
            box.scrollTop = box.scrollHeight;
        });
    }

    // ── Append 1 msg ─────────────────────────────────────────────
    function icAppendMsg(msg, scroll) {
        var box   = document.getElementById('icMsgs');
        // Lấy userId từ JWT trong session — không có sẵn ở JS, dùng so sánh role
        // Ta dùng tên sender: nếu senderRole là role của user hiện tại VÀ không có
        // cách tốt hơn từ session, ta tạm thời so sánh bằng cách fetch lần đầu.
        var isMine = (msg.senderId === icMyUserId);
        var cls    = isMine ? 'mine' : 'other';
        var wrap   = document.createElement('div');
        wrap.className = 'ic-msg ' + cls;
        var av  = document.createElement('div');
        av.className = 'ic-av';
        av.textContent = isMine ? 'T' : (msg.senderName ? msg.senderName[0].toUpperCase() : '?');
        var col  = document.createElement('div');
        col.className = 'ic-msg-body';
        var bub  = document.createElement('div');
        bub.className = 'ic-bub';
        bub.textContent = msg.content;
        var meta = document.createElement('div');
        meta.className = 'ic-meta';
        var d = new Date(msg.sentAt);
        meta.textContent = (!isMine ? msg.senderName + ' · ' : '') +
            d.getHours().toString().padStart(2,'0') + ':' + d.getMinutes().toString().padStart(2,'0');
        col.appendChild(bub); col.appendChild(meta);
        wrap.appendChild(av); wrap.appendChild(col);
        box.appendChild(wrap);
        if (scroll) box.scrollTop = box.scrollHeight;
    }

    // ── Send ──────────────────────────────────────────────────────
    window.icSend = function () {
        if (!icConvId) return;
        var input = document.getElementById('icInput');
        var msg = input.value.trim();
        if (!msg) return;
        // Hiển thị ngay (optimistic UI)
        icAppendMsg({ senderId: icMyUserId, content: msg, sentAt: new Date().toISOString(), conversationId: icConvId }, true);
        input.value = '';
        if (icHubConn && icHubConn.state === 'Connected') {
            icHubConn.invoke('SendMessage', icConvId, msg)
            .catch(function(){ icSendRest(msg); });
        } else { icSendRest(msg); }
    };

    function icSendRest(content) {
        // Tin đã hiển thị optimistic, chỉ lưu lên server
        fetch('/api/msgs/conversations/' + icConvId + '/send', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ content: content })
        }).catch(function() {});
    }

    // ── SignalR ───────────────────────────────────────────────────
    var icMyUserId = 0;

    function _icFetchToken() {
        return fetch('/api/msgs/token').then(function(r){ return r.json(); }).then(function(d){ return d.token || ''; });
    }

    function icConnectHub() {
        _icFetchToken().then(function(token) {
            if (!token) return;

            // Decode JWT để lấy userId (sub claim)
            try {
                var parts = token.split('.');
                if (parts.length === 3) {
                    var payload = JSON.parse(atob(parts[1].replace(/-/g,'+').replace(/_/g,'/')));
                    icMyUserId = parseInt(payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
                                 || payload['sub'] || '0');
                    if (icConvId) icLoadMessages(icConvId);
                }
            } catch(e){}

            icHubConn = new signalR.HubConnectionBuilder()
                .withUrl('https://localhost:7100/hubs/chat', {
                    accessTokenFactory: function() { return _icFetchToken(); }
                })
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .build();

            icHubConn.on('ReceiveMessage', function(msg) {
                if (msg.id && _icSeenIds.has(msg.id)) return;
                if (msg.id) _icSeenIds.add(msg.id);
                if (msg.senderId === icMyUserId) return;

                if (msg.conversationId === icConvId) {
                    icAppendMsg(msg, true);
                    fetch('/api/msgs/conversations/' + msg.conversationId + '/read', { method: 'PUT' });
                } else {
                    var b = document.getElementById('icBadge');
                    b.textContent = (parseInt(b.textContent || '0') + 1);
                    b.classList.add('show');
                    icLoadConversations();
                }
            });

            icHubConn.onreconnected(function() {
                if (icConvId) icHubConn.invoke('JoinConversation', icConvId).catch(function(){});
            });
            icHubConn.start()
            .then(function() {
                if (icConvId) icHubConn.invoke('JoinConversation', icConvId).catch(function(){});
            })
            .catch(function(err){ console.warn('IC SignalR:', err); });
        });
    }

    // Polling fallback — tải tin mới nếu conversation đang mở
    function icPollNewMessages() {
        if (!icConvId) return;
        fetch('/api/msgs/conversations/' + icConvId + '/messages?skip=0&take=1')
        .then(function(r){ return r.json(); })
        .then(function(msgs) {
            if (!msgs || !msgs.length) return;
            var latest = msgs[msgs.length - 1];
            var latestId = latest.id || latest.Id || 0;
            if (latestId && latestId !== _icLastMsgId) {
                _icLastMsgId = latestId;
                icLoadMessages(icConvId);
            }
        }).catch(function(){});
    }

    // ── Modal chọn người chat ────────────────────────────────────
    window.icOpenNewModal = function () {
        fetch('/api/msgs/internal-users')
        .then(function(r){ return r.json(); })
        .then(function(users) {
            var sel = document.getElementById('icTargetSel');
            sel.innerHTML = '<option value="">-- Chọn người nhận --</option>';
            users.forEach(function(u) {
                var o = document.createElement('option');
                o.value = u.id;
                o.textContent = u.name + ' (' + (u.role === 'Doctor' ? 'Bác sĩ' : u.role === 'Staff' ? 'Lễ tân' : 'Quản trị') + ')';
                sel.appendChild(o);
            });
            document.getElementById('icModalBg').classList.add('open');
        });
    };

    window.icCloseModal = function () {
        document.getElementById('icModalBg').classList.remove('open');
    };

    window.icStartChat = function () {
        var targetId = parseInt(document.getElementById('icTargetSel').value);
        if (!targetId) { alert('Vui lòng chọn người nhận.'); return; }
        fetch('/api/msgs/conversations/internal', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ targetUserId: targetId })
        })
        .then(function(r){ return r.json(); })
        .then(function(conv) {
            icCloseModal();
            icLoadConversations();
            setTimeout(function() {
                var item = document.querySelector('[data-conv-id="' + conv.id + '"]');
                if (item) item.click();
            }, 400);
        });
    };

    // ── Load unread badge khi khởi động ──────────────────────────
    fetch('/api/msgs/unread')
    .then(function(r){ return r.json(); })
    .then(function(d){
        if (d.count > 0) {
            var b = document.getElementById('icBadge');
            b.textContent = d.count; b.classList.add('show');
        }
    }).catch(function(){});

    function icEsc(s) {
        if (!s) return '';
        return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
    }
})();
