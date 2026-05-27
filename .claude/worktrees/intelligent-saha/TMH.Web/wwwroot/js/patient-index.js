window.onerror=function(m,s,l,c,e){console.error('[Patient JS Error]',m,'line:'+l,e);return false;};
var csrf      = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
var allAppts  = [];

// norm() \u2014 chu\u1EA9n h\u00F3a field name: ch\u1EA5p nh\u1EADn c\u1EA3 camelCase (API \u0111\u00E3 config) l\u1EABn PascalCase (fallback)
function norm(a) {
    return {
        id           : a.id            != null ? a.id            : a.Id,
        bookingCode  : a.bookingCode   != null ? a.bookingCode   : (a.BookingCode  || ''),
        patientName  : a.patientName   != null ? a.patientName   : (a.PatientName  || ''),
        doctorName   : a.doctorName    != null ? a.doctorName    : (a.DoctorName   || ''),
        specialty    : a.specialty     != null ? a.specialty     : (a.Specialty    || ''),
        workDate     : a.workDate      != null ? a.workDate      : a.WorkDate,
        startTime    : a.startTime     != null ? a.startTime     : (a.StartTime    || ''),
        endTime      : a.endTime       != null ? a.endTime       : (a.EndTime      || ''),
        status       : a.status        != null ? a.status        : (a.Status       || ''),
        statusDisplay: a.statusDisplay != null ? a.statusDisplay : (a.StatusDisplay|| ''),
        note         : a.note         !== undefined ? a.note      : (a.Note         || null),
        diagnosis    : a.diagnosis    !== undefined ? a.diagnosis : (a.Diagnosis    || null),
        bookedAt     : a.bookedAt      != null ? a.bookedAt      : a.BookedAt
    };
}

try {
    var _raw = JSON.parse(document.getElementById('_apptData').textContent || '[]');
    allAppts = Array.isArray(_raw) ? _raw.map(norm) : [];
} catch(e) { console.error('Parse appointments error:', e); allAppts = []; }
var tabLoaded = {};

var UPCOMING_ST = ['ChoXacNhan','DaXacNhan','DaDen','DangKham'];
var BADGE_MAP = {
    'ChoXacNhan' : ['bc','\u23F3 Ch\u1EDD x\u00E1c nh\u1EADn'],
    'DaXacNhan'  : ['bx','\u2705 \u0110\u00E3 x\u00E1c nh\u1EADn'],
    'DaDen'      : ['bd','\uD83C\uDFE5 \u0110\u00E3 \u0111\u1EBFn'],
    'DangKham'   : ['bk','\uD83E\uDE7A \u0110ang kh\u00E1m'],
    'HoanThanh'  : ['bh','\u2714 Ho\u00E0n th\u00E0nh'],
    'DaHuy'      : ['by','\u274C \u0110\u00E3 hu\u1EF7'],
    'VangMat'    : ['bv','\uD83D\uDEAB V\u1EAFng m\u1EB7t']
};

// \u2500\u2500 Utilities \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
function showToast(msg, ok) {
    var t = document.getElementById('toast');
    t.textContent = msg;
    t.className = 'toast ' + (ok ? 'ok' : 'err');
    t.style.cssText = 'display:block;animation:fadeSlide .3s ease';
    clearTimeout(t._timer);
    t._timer = setTimeout(function(){ t.style.display='none'; }, 3500);
}

function fmtDate(s) {
    var d = new Date(s);
    return {
        day  : d.getDate().toString().padStart(2,'0'),
        mon  : d.toLocaleString('vi-VN',{month:'short'}).replace('.',''),
        full : d.toLocaleDateString('vi-VN'),
        iso  : d.toISOString().substring(0,10)
    };
}

function calcAge(dob) {
    return Math.floor((Date.now() - new Date(dob)) / 31557600000);
}

function initials(name) {
    if (!name) return '?';
    var parts = name.trim().split(' ');
    return parts[parts.length-1].charAt(0).toUpperCase();
}

// \u2500\u2500 Tab switching \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
function switchTab(name, btn) {
    ['schedule','results','relatives','profile','notifs'].forEach(function(t){
        document.getElementById('tab-'+t).style.display = (t===name) ? '' : 'none';
    });
    document.querySelectorAll('.pt-tab').forEach(function(b){ b.classList.remove('active'); });
    btn.classList.add('active');
    if (!tabLoaded[name]) { tabLoaded[name] = true; loadTab(name); }
}

function loadTab(name) {
    if (name === 'results')   renderResults();
    if (name === 'relatives') loadRelatives();
    if (name === 'profile')   loadPersonal();
    if (name === 'notifs')    loadNotifs();
}

// \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550
// TAB 1: L\u1ECACH KH\u00C1M
// \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550
function renderApptCard(a, canCancel) {
    if (!Array.isArray(allAppts)) return '';
    var dt  = fmtDate(a.workDate);
    var bg  = BADGE_MAP[a.status] || ['bc', a.statusDisplay || a.status];
    var acts = '';

    if (canCancel && (a.status === 'ChoXacNhan' || a.status === 'DaXacNhan'))
        acts += '<button class="btn-sm btn-danger" onclick="cancelAppt('+a.id+',this)">Hu\u1EF7 l\u1ECBch</button>';
    if (a.status === 'HoanThanh' || a.status === 'DaHuy')
        acts += '<a class="btn-sm btn-primary-sm" href="/Booking">\u0110\u1EB7t l\u1EA1i</a>';

    return '<div class="appt-card" id="ac-'+a.id+'">'
        + '<div class="appt-date-box"><div class="appt-date-day">'+dt.day+'</div><div class="appt-date-month">'+dt.mon+'</div></div>'
        + '<div class="appt-body">'
            + '<div class="appt-doc">'+(a.doctorName||'')+'<span class="badge '+bg[0]+'">'+bg[1]+'</span></div>'
            + '<div class="appt-spec">'+(a.specialty||'Tai M\u0169i H\u1ECDng')+'</div>'
            + '<div class="appt-meta">'
                + '<span>\uD83D\uDD50 '+(a.startTime||'')+' \u2013 '+(a.endTime||'')+'</span>'
                + '<span>\uD83D\uDCC5 '+dt.full+'</span>'
                + '<span class="appt-code">#'+(a.bookingCode||'')+'</span>'
                + (a.patientName ? '<span>\uD83D\uDC64 '+a.patientName+'</span>' : '')
            + '</div>'
            + (a.note ? '<div class="appt-note">\uD83D\uDCDD <strong>Ghi ch\u00FA:</strong> '+a.note+'</div>' : '')
            + (a.diagnosis ? '<div class="appt-diag">\uD83E\uDE7A <strong>Ch\u1EA9n \u0111o\u00E1n:</strong> '+a.diagnosis+'</div>' : '')
        + '</div>'
        + (acts ? '<div class="appt-actions">'+acts+'</div>' : '')
        + '</div>';
}

function initSchedule() {
    if (!Array.isArray(allAppts)) allAppts = [];
    var up   = allAppts.filter(function(a){ return UPCOMING_ST.indexOf(a.status) >= 0; })
                       .sort(function(a,b){ return new Date(a.workDate)-new Date(b.workDate); });
    var hist = allAppts.filter(function(a){ return UPCOMING_ST.indexOf(a.status) < 0; })
                       .sort(function(a,b){ return new Date(b.workDate)-new Date(a.workDate); });
    var done = allAppts.filter(function(a){ return a.status==='HoanThanh'; });
    var can  = allAppts.filter(function(a){ return a.status==='DaHuy'||a.status==='VangMat'; });

    document.getElementById('stTotal').textContent  = allAppts.length;
    document.getElementById('stUp').textContent     = up.length;
    document.getElementById('stDone').textContent   = done.length;
    document.getElementById('stCancel').textContent = can.length;

    var upEl = document.getElementById('upcomingList');
    upEl.innerHTML = up.length
        ? up.map(function(a){ return renderApptCard(a, true); }).join('')
        : '<div class="empty"><div class="empty-ico">\uD83D\uDCC5</div><div class="empty-ttl">Ch\u01B0a c\u00F3 l\u1ECBch kh\u00E1m s\u1EAFp t\u1EDBi</div><div class="empty-desc">\u0110\u1EB7t l\u1ECBch ngay \u0111\u1EC3 \u0111\u01B0\u1EE3c kh\u00E1m b\u1EDFi \u0111\u1ED9i ng\u0169 b\u00E1c s\u0129 chuy\u00EAn khoa TMH</div><a href="/Booking" class="btn-primary-sm btn-sm" style="margin:0 auto">\u0110\u1EB7t l\u1ECBch ngay</a></div>';

    var hiEl = document.getElementById('historyList');
    hiEl.innerHTML = hist.length
        ? hist.map(function(a){ return renderApptCard(a, false); }).join('')
        : '<div class="empty"><div class="empty-ico">\uD83D\uDD50</div><div class="empty-ttl">Ch\u01B0a c\u00F3 l\u1ECBch s\u1EED kh\u00E1m</div></div>';
}

function cancelAppt(id, btn) {
    if (!confirm('X\u00E1c nh\u1EADn hu\u1EF7 l\u1ECBch kh\u00E1m n\u00E0y?')) return;
    btn.disabled = true; btn.textContent = '\u0110ang hu\u1EF7...';
    fetch('/Patient/Cancel?id='+id, { method:'POST', headers:{'RequestVerificationToken':csrf} })
    .then(function(r){ return r.json(); })
    .then(function(d){
        if (d.success || d.Success) {
            showToast('\u0110\u00E3 hu\u1EF7 l\u1ECBch th\u00E0nh c\u00F4ng.', true);
            // Fade out card, sau đó fetch lại toàn bộ data từ server và re-render
            var card = document.getElementById('ac-'+id);
            if (card) { card.style.opacity='0'; card.style.transition='opacity .3s'; }
            setTimeout(function(){
                reloadAppointments();
            }, 350);
        } else {
            showToast(d.message || 'Kh\u00F4ng th\u1EC3 hu\u1EF7.', false);
            btn.disabled = false; btn.textContent = 'Hu\u1EF7 l\u1ECBch';
        }
    }).catch(function(){ showToast('L\u1ED7i k\u1EBFt n\u1ED1i.', false); btn.disabled=false; btn.textContent='Hu\u1EF7 l\u1ECBch'; });
}

// Fetch lại danh sách lịch khám từ server rồi re-render tab Lịch khám + Kết quả
function reloadAppointments() {
    fetch('/Patient/GetMyAppointments')
    .then(function(r){ return r.json(); })
    .then(function(data){
        allAppts = Array.isArray(data) ? data.map(norm) : [];
        initSchedule();
        // Nếu tab Kết quả đã load rồi thì re-render luôn
        if (tabLoaded['results']) renderResults();
    })
    .catch(function(){ showToast('Kh\u00F4ng th\u1EC3 t\u1EA3i l\u1EA1i d\u1EEF li\u1EC7u.', false); });
}

// \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550
// TAB 2: K\u1EBET QU\u1EA2 KH\u00C1M
// \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550
function renderResults() {
    var done = Array.isArray(allAppts)
        ? allAppts.filter(function(a){ return a.status==='HoanThanh'; })
                  .sort(function(a,b){ return new Date(b.workDate)-new Date(a.workDate); })
        : [];
    var el = document.getElementById('resultsList');

    if (!done.length) {
        el.innerHTML = '<div class="empty"><div class="empty-ico">\uD83E\uDE7A</div><div class="empty-ttl">Ch\u01B0a c\u00F3 k\u1EBFt qu\u1EA3 kh\u00E1m</div><div class="empty-desc">K\u1EBFt qu\u1EA3 s\u1EBD xu\u1EA5t hi\u1EC7n t\u1EA1i \u0111\u00E2y sau khi b\u00E1c s\u0129 ho\u00E0n th\u00E0nh kh\u00E1m v\u00E0 ghi ch\u1EA9n \u0111o\u00E1n</div></div>';
        return;
    }

    el.innerHTML = done.map(function(a) {
        var dt = fmtDate(a.workDate);
        var diagHtml = a.diagnosis
            ? '<div class="result-diag"><div class="rd-label">\uD83E\uDE7A K\u1EBFt qu\u1EA3 ch\u1EA9n \u0111o\u00E1n</div><div class="rd-text">'+a.diagnosis+'</div></div>'
            : '<div class="result-no-diag">B\u00E1c s\u0129 ch\u01B0a c\u1EADp nh\u1EADt ch\u1EA9n \u0111o\u00E1n</div>';
        return '<div class="result-card" id="rc-'+a.id+'">'
            + '<div class="result-head">'
                + '<div class="result-head-left">'
                    + '<div class="rdoc">'+(a.doctorName||'')+' \u2014 '+(a.specialty||'Tai M\u0169i H\u1ECDng')+'</div>'
                    + '<div class="rdate"><span>\uD83D\uDCC5 '+dt.full+'</span><span>\uD83D\uDD50 '+(a.startTime||'')+'</span><span class="appt-code">#'+(a.bookingCode||'')+'</span></div>'
                + '</div>'
                + '<button class="btn-print" data-card="rc-'+a.id+'" data-code="'+a.bookingCode+'" onclick="printResult(this.dataset.card,this.dataset.code)">\uD83D\uDDA8 In phi\u1EBFu</button>'
            + '</div>'
            + '<div class="result-body">'
                + '<div class="result-grid">'
                    + '<div class="result-field"><div class="rf-lbl">B\u1EC7nh nh\u00E2n</div><div class="rf-val">'+(a.patientName||'\u2014')+'</div></div>'
                    + '<div class="result-field"><div class="rf-lbl">Ng\u00E0y kh\u00E1m</div><div class="rf-val">'+dt.full+'</div></div>'
                    + '<div class="result-field"><div class="rf-lbl">B\u00E1c s\u0129 ph\u1EE5 tr\u00E1ch</div><div class="rf-val">'+(a.doctorName||'\u2014')+'</div></div>'
                    + '<div class="result-field"><div class="rf-lbl">Chuy\u00EAn khoa</div><div class="rf-val">'+(a.specialty||'Tai M\u0169i H\u1ECDng')+'</div></div>'
                    + (a.note ? '<div class="result-field" style="grid-column:span 2"><div class="rf-lbl">Tri\u1EC7u ch\u1EE9ng / Ghi ch\u00FA</div><div class="rf-val">'+a.note+'</div></div>' : '')
                + '</div>'
                + diagHtml
            + '</div></div>';
    }).join('');
}

function printResult(cardId, code) {
    var card = document.getElementById(cardId);
    if (!card) return;
    var win = window.open('', '_blank', 'width=700,height=900');
    win.document.write('<!DOCTYPE html><html><head><title>Phi\u1EBFu kh\u00E1m #'+code+'</title>');
    win.document.write('<meta charset="utf-8"><style>');
    win.document.write('*{box-sizing:border-box}body{font-family:Arial,sans-serif;padding:36px;max-width:620px;margin:0 auto;color:#222}');
    win.document.write('.clinic-name{font-size:20px;font-weight:700;color:#0a4d7c;text-align:center}');
    win.document.write('.clinic-sub{font-size:12px;color:#666;text-align:center;margin:4px 0 0}');
    win.document.write('.divider{border:none;border-top:2px solid #0a4d7c;margin:18px 0}');
    win.document.write('.title{font-size:16px;font-weight:700;text-align:center;text-transform:uppercase;letter-spacing:.06em;margin-bottom:22px}');
    win.document.write('.grid{display:grid;grid-template-columns:1fr 1fr;gap:10px 24px;margin-bottom:20px}');
    win.document.write('.field .lbl{font-size:11px;font-weight:700;color:#555;text-transform:uppercase;margin-bottom:3px}');
    win.document.write('.field .val{font-size:13px;color:#000}');
    win.document.write('.diag{background:#f0faf4;border-left:4px solid #2d7a52;padding:14px 16px;border-radius:4px;margin-top:6px}');
    win.document.write('.diag-title{font-size:12px;font-weight:700;color:#2d7a52;text-transform:uppercase;margin-bottom:6px}');
    win.document.write('.diag-text{font-size:13px;color:#1a4a2d;line-height:1.6}');
    win.document.write('.footer{display:flex;justify-content:space-between;margin-top:32px;padding-top:18px;border-top:1px solid #ddd;font-size:12px;color:#666}');
    win.document.write('.sig{text-align:center}');
    win.document.write('.sig-name{font-size:13px;font-weight:700;margin-top:40px;border-top:1px solid #333;padding-top:4px;display:inline-block}');
    win.document.write('</style></head><body>');
    win.document.write('<div class="clinic-name">PH\u00D2NG KH\u00C1M TAI M\u0168I H\u1ECCNG</div>');
    win.document.write('<div class="clinic-sub">123 \u0110\u01B0\u1EDDng Y T\u1EBF, Q.1, TP.HCM &nbsp;|&nbsp; Hotline: 1800 5678 &nbsp;|&nbsp; Email: info@@pktatmuihong.vn</div>');
    win.document.write('<hr class="divider">');
    win.document.write('<div class="title">Phi\u1EBFu k\u1EBFt qu\u1EA3 kh\u00E1m b\u1EC7nh</div>');

    var fields = card.querySelectorAll('.result-field');
    win.document.write('<div class="grid">');
    fields.forEach(function(f){
        var lbl = f.querySelector('.rf-lbl'), val = f.querySelector('.rf-val');
        if (lbl && val) win.document.write('<div class="field"><div class="lbl">'+lbl.textContent+'</div><div class="val">'+val.textContent+'</div></div>');
    });
    win.document.write('</div>');

    var diag = card.querySelector('.result-diag');
    var noDiag = card.querySelector('.result-no-diag');
    if (diag) {
        win.document.write('<div class="diag"><div class="diag-title">K\u1EBFt qu\u1EA3 ch\u1EA9n \u0111o\u00E1n</div>');
        win.document.write('<div class="diag-text">' + (card.querySelector('.rd-text') ? card.querySelector('.rd-text').textContent : '') + '</div></div>');
    } else if (noDiag) {
        win.document.write('<div class="diag" style="background:#fafafa;border-color:#ddd"><div class="diag-text" style="color:#999">Ch\u01B0a c\u00F3 ch\u1EA9n \u0111o\u00E1n</div></div>');
    }

    win.document.write('<div class="footer"><span>Ng\u00E0y in: '+new Date().toLocaleDateString('vi-VN')+'</span>');
    win.document.write('<div class="sig"><div>B\u00E1c s\u0129 \u0111i\u1EC1u tr\u1ECB</div><div class="sig-name">&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</div></div></div>');
    win.document.write('</body></html>');
    win.document.close();
    setTimeout(function(){ win.print(); }, 600);
}

// \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550
// TAB 3: H\u1ED2 S\u01A0 NG\u01AF\u1EDCI TH\u00C2N
// \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550
var relatives = [];

function loadRelatives() {
    var el = document.getElementById('relativesList');
    el.innerHTML = '<div class="spin">\u0110ang t\u1EA3i h\u1ED3 s\u01A1...</div>';
    fetch('/Patient/GetMyProfiles')
    .then(function(r){ return r.json(); })
    .then(function(data){
        relatives = Array.isArray(data) ? data : [];
        renderRelatives();
    })
    .catch(function(){ el.innerHTML = '<div class="empty"><div class="empty-ico">\u26A0\uFE0F</div><div class="empty-ttl">L\u1ED7i t\u1EA3i d\u1EEF li\u1EC7u</div><div class="empty-desc">Vui l\u00F2ng th\u1EED l\u1EA1i sau.</div></div>'; });
}

function renderRelatives() {
    var el = document.getElementById('relativesList');
    if (!relatives.length) {
        el.innerHTML = '<div class="empty" style="margin-bottom:14px"><div class="empty-ico">\uD83D\uDC68\uD83D\uDC69\uD83D\uDC67</div><div class="empty-ttl">Ch\u01B0a c\u00F3 h\u1ED3 s\u01A1 ng\u01B0\u1EDDi th\u00E2n</div><div class="empty-desc">T\u1EA1o h\u1ED3 s\u01A1 \u0111\u1EC3 \u0111\u1EB7t l\u1ECBch kh\u00E1m cho c\u00E1c th\u00E0nh vi\u00EAn trong gia \u0111\u00ECnh</div></div>';
        return;
    }
    var colors = ['#0a4d7c','#1a6fa8','#2d7a52','#7c3d0a','#5a0a7c'];
    el.innerHTML = '<div class="profile-grid">'
        + relatives.map(function(p, i){
            var age = calcAge(p.dateOfBirth);
            var ini = initials(p.fullName);
            var col = colors[i % colors.length];
            return '<div class="profile-card">'
                + '<div class="profile-avatar" style="background:linear-gradient(135deg,'+col+','+col+'cc)">'+ini+'</div>'
                + '<div class="profile-info">'
                    + '<div class="profile-name">'+p.fullName+'</div>'
                    + '<div class="profile-meta">'+p.gender+' \u00B7 '+age+' tu\u1ED5i \u00B7 <span class="profile-code">'+p.recordCode+'</span></div>'
                    + (p.medicalHistory ? '<div class="profile-history">\uD83D\uDCCB '+p.medicalHistory+'</div>' : '')
                    + '<div class="profile-acts">'
                        + '<button class="btn-sm btn-outline" onclick="openProfileForm('+p.id+')">\u270F S\u1EEDa</button>'
                        + '<button class="btn-sm btn-danger" data-id="'+p.id+'" data-name="'+p.fullName.replace(/"/g,'&quot;')+'" onclick="deleteProfile(+this.dataset.id,this.dataset.name)">\uD83D\uDDD1 Xo\u00E1</button>'
                        + '<a class="btn-sm btn-primary-sm" href="/Booking">\uD83D\uDCC5 \u0110\u1EB7t l\u1ECBch</a>'
                    + '</div>'
                + '</div></div>';
        }).join('')
        + '</div>';
}

function openProfileForm(id) {
    document.getElementById('pfErr').style.display = 'none';
    document.getElementById('pfOk').style.display  = 'none';
    document.getElementById('pfId').value = id || 0;
    document.getElementById('profileModalTitle').textContent = id ? '\u270F\uFE0F S\u1EEDa h\u1ED3 s\u01A1' : '\uD83D\uDC68\uD83D\uDC69\uD83D\uDC67 Th\u00EAm h\u1ED3 s\u01A1 ng\u01B0\u1EDDi th\u00E2n';

    if (id) {
        var p = relatives.find(function(r){ return r.id === id; });
        if (p) {
            document.getElementById('pfName').value    = p.fullName || '';
            document.getElementById('pfDob').value     = p.dateOfBirth ? p.dateOfBirth.substring(0,10) : '';
            document.getElementById('pfGender').value  = p.gender || '';
            document.getElementById('pfHistory').value = p.medicalHistory || '';
        }
    } else {
        document.getElementById('pfName').value    = '';
        document.getElementById('pfDob').value     = '';
        document.getElementById('pfGender').value  = '';
        document.getElementById('pfHistory').value = '';
    }
    document.getElementById('profileModal').classList.add('open');
}
function closeProfileForm() { document.getElementById('profileModal').classList.remove('open'); }

function saveProfile() {
    var name = document.getElementById('pfName').value.trim();
    var dob  = document.getElementById('pfDob').value;
    var gen  = document.getElementById('pfGender').value;
    var err  = document.getElementById('pfErr');
    err.style.display = 'none';

    if (!name) { err.textContent='Vui l\u00F2ng nh\u1EADp h\u1ECD v\u00E0 t\u00EAn.'; err.style.display='block'; return; }
    if (!dob)  { err.textContent='Vui l\u00F2ng nh\u1EADp ng\u00E0y sinh.'; err.style.display='block'; return; }
    if (!gen)  { err.textContent='Vui l\u00F2ng ch\u1ECDn gi\u1EDBi t\u00EDnh.'; err.style.display='block'; return; }

    var dto = {
        id            : parseInt(document.getElementById('pfId').value) || 0,
        fullName      : name,
        dateOfBirth   : dob,
        gender        : gen,
        medicalHistory: document.getElementById('pfHistory').value || null
    };

    var btn = document.querySelector('#profileModal .btn-modal-save');
    btn.disabled = true; btn.textContent = '\u0110ang l\u01B0u...';

    fetch('/Patient/SaveProfile', {
        method:'POST',
        headers:{'Content-Type':'application/json','RequestVerificationToken':csrf},
        body: JSON.stringify(dto)
    })
    .then(function(r){ return r.json(); })
    .then(function(d){
        btn.disabled = false; btn.textContent = '\uD83D\uDCBE L\u01B0u h\u1ED3 s\u01A1';
        if (d.success || d.Success) {
            document.getElementById('pfOk').textContent = 'L\u01B0u h\u1ED3 s\u01A1 th\u00E0nh c\u00F4ng!';
            document.getElementById('pfOk').style.display = 'block';
            setTimeout(function(){ closeProfileForm(); loadRelatives(); }, 1200);
        } else {
            err.textContent = d.message || d.Message || 'C\u00F3 l\u1ED7i x\u1EA3y ra.';
            err.style.display = 'block';
        }
    })
    .catch(function(){ btn.disabled=false; btn.textContent='\uD83D\uDCBE L\u01B0u h\u1ED3 s\u01A1'; err.textContent='L\u1ED7i k\u1EBFt n\u1ED1i.'; err.style.display='block'; });
}

function deleteProfile(id, name) {
    if (!confirm('X\u00E1c nh\u1EADn xo\u00E1 h\u1ED3 s\u01A1 "'+name+'"?\nH\u00E0nh \u0111\u1ED9ng n\u00E0y kh\u00F4ng th\u1EC3 ho\u00E0n t\u00E1c.')) return;
    fetch('/Patient/DeleteProfile?id='+id, { method:'POST', headers:{'RequestVerificationToken':csrf} })
    .then(function(r){ return r.json(); })
    .then(function(d){
        if (d.success || d.Success) { showToast('\u0110\u00E3 xo\u00E1 h\u1ED3 s\u01A1 th\u00E0nh c\u00F4ng.', true); loadRelatives(); }
        else showToast(d.message || 'Kh\u00F4ng th\u1EC3 xo\u00E1 h\u1ED3 s\u01A1.', false);
    })
    .catch(function(){ showToast('L\u1ED7i k\u1EBFt n\u1ED1i.', false); });
}

// \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550
// TAB 4: H\u1ED2 S\u01A0 C\u00C1 NH\u00C2N
// \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550
var personalData = {};

function loadPersonal() {
    fetch('/Patient/GetProfile')
    .then(function(r){ return r.json(); })
    .then(function(u){
        personalData = u;
        renderPersonal(u);
    })
    .catch(function(){
        document.getElementById('personalCard').innerHTML = '<div class="empty"><div class="empty-ico">\u26A0\uFE0F</div><div class="empty-ttl">L\u1ED7i t\u1EA3i th\u00F4ng tin</div><div class="empty-desc">Vui l\u00F2ng th\u1EED l\u1EA1i sau.</div></div>';
    });
}

function renderPersonal(u) {
    var ini = ((u.hoTenDem||'').split(' ').pop()||'').charAt(0).toUpperCase()
            + (u.ten||'').charAt(0).toUpperCase();
    document.getElementById('personalCard').innerHTML =
        '<div class="personal-header">'
            + '<div class="personal-avatar">'+ini+'</div>'
            + '<div style="flex:1">'
                + '<div class="personal-name">'+(u.fullName||u.username||'')+'</div>'
                + '<div class="personal-role">B\u1EC7nh nh\u00E2n \u00B7 '+(u.email||'')+'</div>'
            + '</div>'
            + '<button class="btn-sm btn-outline" onclick="openPersonalForm()">\u270F Ch\u1EC9nh s\u1EEDa</button>'
        + '</div>'
        + '<div class="personal-fields">'
            + pfRow('H\u1ECD v\u00E0 t\u00EAn \u0111\u1EC7m', u.hoTenDem)
            + pfRow('T\u00EAn', u.ten)
            + pfRow('S\u1ED1 \u0111i\u1EC7n tho\u1EA1i', u.phone)
            + pfRow('Email', u.email)
            + pfRow('Ng\u00E0y sinh', u.ngaySinh ? new Date(u.ngaySinh).toLocaleDateString('vi-VN') : null)
            + pfRow('Gi\u1EDBi t\u00EDnh', u.gioiTinh)
            + pfRow('Nh\u00F3m m\u00E1u', u.nhomMau ? '<span class="pf-tag">\uD83E\uDE78 '+u.nhomMau+'</span>' : null)
            + pfRow('\u0110\u1ECBa ch\u1EC9', u.diaChi)
        + '</div>';
}

function pfRow(label, val) {
    var isHtml = val && val.includes('<');
    return '<div class="pf-item">'
        + '<div class="pf-lbl">'+label+'</div>'
        + '<div class="pf-val '+(val?'':'pf-empty')+'">'
            + (val ? (isHtml ? val : val) : 'Ch\u01B0a c\u1EADp nh\u1EADt')
        + '</div></div>';
}

function openPersonalForm() {
    document.getElementById('pmHoTenDem').value = personalData.hoTenDem || '';
    document.getElementById('pmTen').value       = personalData.ten || '';
    document.getElementById('pmDob').value       = personalData.ngaySinh ? personalData.ngaySinh.substring(0,10) : '';
    document.getElementById('pmGender').value    = personalData.gioiTinh || '';
    document.getElementById('pmBlood').value     = personalData.nhomMau || '';
    document.getElementById('pmAddr').value      = personalData.diaChi || '';
    document.getElementById('pmPhone').value     = personalData.phone || '';
    document.getElementById('pmErr').style.display = 'none';
    document.getElementById('pmOk').style.display  = 'none';
    document.getElementById('personalModal').classList.add('open');
}
function closePersonalForm() { document.getElementById('personalModal').classList.remove('open'); }

function savePersonal() {
    var dto = {
        hoTenDem : document.getElementById('pmHoTenDem').value || null,
        ten      : document.getElementById('pmTen').value || null,
        ngaySinh : document.getElementById('pmDob').value || null,
        gioiTinh : document.getElementById('pmGender').value || null,
        nhomMau  : document.getElementById('pmBlood').value || null,
        diaChi   : document.getElementById('pmAddr').value || null
    };
    var btn = document.querySelector('#personalModal .btn-modal-save');
    btn.disabled = true; btn.textContent = '\u0110ang l\u01B0u...';

    fetch('/Patient/UpdateProfile', {
        method:'POST',
        headers:{'Content-Type':'application/json','RequestVerificationToken':csrf},
        body: JSON.stringify(dto)
    })
    .then(function(r){ return r.json(); })
    .then(function(d){
        btn.disabled = false; btn.textContent = '\uD83D\uDCBE L\u01B0u thay \u0111\u1ED5i';
        if (d.success || d.Success) {
            document.getElementById('pmOk').textContent = 'C\u1EADp nh\u1EADt th\u00E0nh c\u00F4ng!';
            document.getElementById('pmOk').style.display = 'block';
            setTimeout(function(){ closePersonalForm(); tabLoaded['profile']=false; loadPersonal(); }, 1200);
        } else {
            document.getElementById('pmErr').textContent = d.message || 'C\u00F3 l\u1ED7i x\u1EA3y ra.';
            document.getElementById('pmErr').style.display = 'block';
        }
    })
    .catch(function(){ btn.disabled=false; btn.textContent='\uD83D\uDCBE L\u01B0u thay \u0111\u1ED5i'; document.getElementById('pmErr').textContent='L\u1ED7i k\u1EBFt n\u1ED1i.'; document.getElementById('pmErr').style.display='block'; });
}

// \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550
// TAB 5: TH\u00D4NG B\u00C1O
// \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550
var notifData = [];
var NOTIF_ICONS = { 'XacNhanLich':'\u2705','NhacLich':'\uD83D\uDD14','HuyLich':'\u274C','DoiLich':'\uD83D\uDD04' };

function loadNotifs() {
    document.getElementById('notifList').innerHTML = '<div class="spin">\u0110ang t\u1EA3i th\u00F4ng b\u00E1o...</div>';
    fetch('/Patient/GetNotifications')
    .then(function(r){ return r.json(); })
    .then(function(data){
        notifData = Array.isArray(data) ? data : [];
        renderNotifs();
        // Hi\u1EC3n th\u1ECB badge
        var unread = notifData.filter(function(n){ return !n.isRead; }).length;
        var badge = document.getElementById('notifBadge');
        if (unread > 0) { badge.textContent = unread; badge.style.display='inline-flex'; }
    })
    .catch(function(){ document.getElementById('notifList').innerHTML='<div class="empty"><div class="empty-ico">\u26A0\uFE0F</div><div class="empty-ttl">L\u1ED7i t\u1EA3i th\u00F4ng b\u00E1o</div></div>'; });
}

function renderNotifs() {
    var el = document.getElementById('notifList');
    if (!notifData.length) {
        el.innerHTML = '<div class="empty"><div class="empty-ico">\uD83D\uDD14</div><div class="empty-ttl">Kh\u00F4ng c\u00F3 th\u00F4ng b\u00E1o n\u00E0o</div><div class="empty-desc">Th\u00F4ng b\u00E1o x\u00E1c nh\u1EADn l\u1ECBch kh\u00E1m v\u00E0 nh\u1EAFc l\u1ECBch s\u1EBD hi\u1EC3n th\u1ECB t\u1EA1i \u0111\u00E2y</div></div>';
        return;
    }
    el.innerHTML = notifData.map(function(n){
        var ico = NOTIF_ICONS[n.type] || '\uD83D\uDCCB';
        return '<div class="notif-item '+(n.isRead?'':'unread')+'" id="nf-'+n.id+'" onclick="readNotif('+n.id+',this)">'
            + '<div class="notif-ico">'+ico+'</div>'
            + '<div class="notif-body">'
                + '<div class="notif-title">'+n.title+'</div>'
                + '<div class="notif-content">'+n.content+'</div>'
                + '<div class="notif-time">\uD83D\uDD50 '+n.sentAt+'</div>'
            + '</div>'
            + (!n.isRead ? '<div class="unread-dot"></div>' : '')
            + '</div>';
    }).join('');
}

function readNotif(id, el) {
    if (!el.classList.contains('unread')) return;
    el.classList.remove('unread');
    var dot = el.querySelector('.unread-dot');
    if (dot) dot.remove();
    // C\u1EADp nh\u1EADt badge
    var unread = document.querySelectorAll('.notif-item.unread').length;
    var badge = document.getElementById('notifBadge');
    if (unread > 0) { badge.textContent = unread; }
    else { badge.style.display='none'; }
    fetch('/Patient/MarkNotifRead?id='+id, { method:'POST', headers:{'RequestVerificationToken':csrf} });
}

function readAllNotifs() {
    var unreadItems = document.querySelectorAll('.notif-item.unread');
    if (!unreadItems.length) { showToast('T\u1EA5t c\u1EA3 \u0111\u00E3 \u0111\u01B0\u1EE3c \u0111\u1ECDc.', true); return; }
    unreadItems.forEach(function(el){
        var idMatch = el.id.match(/nf-(\d+)/);
        if (idMatch) readNotif(parseInt(idMatch[1]), el);
    });
    showToast('\u0110\u00E3 \u0111\u00E1nh d\u1EA5u t\u1EA5t c\u1EA3 \u0111\u00E3 \u0111\u1ECDc.', true);
}

// \u2500\u2500 Kh\u1EDFi \u0111\u1ED9ng \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
initSchedule();

// Load badge th\u00F4ng b\u00E1o n\u1EC1n (kh\u00F4ng c\u1EA7n b\u1EADt tab)
fetch('/Patient/GetNotifications')
.then(function(r){ return r.json(); })
.then(function(data){
    var unread = Array.isArray(data) ? data.filter(function(n){ return !n.isRead; }).length : 0;
    if (unread > 0) {
        var b = document.getElementById('notifBadge');
        b.textContent = unread; b.style.display='inline-flex';
        if (!tabLoaded['notifs']) notifData = data;
    }
}).catch(function(){});