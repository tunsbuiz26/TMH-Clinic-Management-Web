// admin-dashboard.js — Logic cho trang Admin Dashboard
// Tách ra file riêng để tránh lỗi Razor parsing và dễ debug

// ── CSRF token ────────────────────────────────────────────
var csrf = '';
var weeklyChart = null;
var statusChart = null;
var tabLoaded   = {};
var allUsers    = [];
var doctorCache = [];
var allDoctors  = [];

document.addEventListener('DOMContentLoaded', function () {
    var inp = document.querySelector('input[name="__RequestVerificationToken"]');
    if (inp) csrf = inp.value;
    tabLoaded['overview'] = true;
    loadOverview();
});

// ── Toast ─────────────────────────────────────────────────
function showToast(msg, ok) {
    var t = document.getElementById('toast');
    if (!t) return;
    t.textContent = msg;
    t.className = 'toast ' + (ok ? 'ok' : 'err');
    t.style.cssText = 'display:block;animation:fadeUp .3s ease';
    clearTimeout(t._t);
    t._t = setTimeout(function () { t.style.display = 'none'; }, 3500);
}

// ── Tab switching ─────────────────────────────────────────
function switchTab(name, btn) {
    document.querySelectorAll('.tab-pane').forEach(function (p) { p.classList.remove('active'); });
    document.querySelectorAll('.tab-btn').forEach(function (b) { b.classList.remove('active'); });
    document.getElementById('tab-' + name).classList.add('active');
    btn.classList.add('active');
    if (!tabLoaded[name]) { tabLoaded[name] = true; loadTab(name); }
}
function loadTab(name) {
    if (name === 'overview') loadOverview();
    if (name === 'users')    loadUsers();
    if (name === 'doctors')  loadDoctors();
    if (name === 'stats')    initStatsTab();
    if (name === 'schedule') initScheduleTab();
    if (name === 'articles') loadAllArticles();
    if (name === 'inbox')    initAdminInbox();
}

// ══════════════════════════════════════════════════════════
// TAB 1: TỔNG QUAN
// ══════════════════════════════════════════════════════════
function loadOverview() {
    fetch('/Admin/GetStats')
        .then(function (r) { return r.json(); })
        .then(function (d) {
            document.getElementById('sTotal').textContent     = d.totalAppointments ?? d.TotalAppointments ?? '—';
            document.getElementById('sToday').textContent     = d.todayAppointments ?? d.TodayAppointments ?? '—';
            document.getElementById('sPatients').textContent  = d.totalPatients     ?? d.TotalPatients     ?? '—';
            document.getElementById('sDoctors').textContent   = d.activeDoctors     ?? d.ActiveDoctors     ?? '—';
            document.getElementById('sCompleted').textContent = d.completedToday    ?? d.CompletedToday    ?? '—';
            document.getElementById('sCancelled').textContent = d.cancelledToday    ?? d.CancelledToday    ?? '—';
            document.getElementById('sUsers').textContent     = d.totalUsers        ?? d.TotalUsers        ?? '—';
        })
        .catch(function () { showToast('Loi tai thong ke.', false); });

    fetch('/Admin/GetWeeklyStats')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            var labels    = data.map(function (d) { return d.date    || d.Date    || ''; });
            var totals    = data.map(function (d) { return d.total   || d.Total   || 0; });
            var completed = data.map(function (d) { return d.completed || d.Completed || 0; });
            var cancelled = data.map(function (d) { return d.cancelled || d.Cancelled || 0; });

            if (weeklyChart) weeklyChart.destroy();
            var canvas = document.getElementById('weeklyChart');
            var hasData = totals.some(function(v) { return v > 0; });
            if (!hasData) {
                canvas.parentElement.innerHTML = '<div style="display:flex;align-items:center;justify-content:center;height:240px;color:#aab8c2;font-size:13px;">Chưa có dữ liệu lịch khám</div>';
                return;
            }
            weeklyChart = new Chart(canvas, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [
                        { label: 'Tổng',       data: totals,    backgroundColor: 'rgba(26,111,168,.3)',  borderColor: '#1a6fa8', borderWidth: 1, borderRadius: 4 },
                        { label: 'Hoàn thành', data: completed, backgroundColor: 'rgba(45,122,82,.7)',   borderColor: '#2d7a52', borderWidth: 1, borderRadius: 4 },
                        { label: 'Huỷ/Vắng',  data: cancelled, backgroundColor: 'rgba(192,57,43,.5)',   borderColor: '#c0392b', borderWidth: 1, borderRadius: 4 }
                    ]
                },
                options: {
                    responsive: true, maintainAspectRatio: false,
                    plugins: { legend: { position: 'top', labels: { font: { size: 12 } } } },
                    scales: {
                        x: { grid: { display: false } },
                        y: { beginAtZero: true, ticks: { stepSize: 1 }, grid: { color: 'rgba(0,0,0,.05)' } }
                    }
                }
            });
        })
        .catch(function () { showToast('Lỗi tải biểu đồ tuần.', false); });
}

// ══════════════════════════════════════════════════════════
// TAB 2: TAI KHOAN
// ══════════════════════════════════════════════════════════
function loadUsers() {
    document.getElementById('usersTable').innerHTML = '<div class="loading-row">Dang tai...</div>';
    fetch('/Admin/GetUsers')
        .then(function (r) { return r.json(); })
        .then(function (users) { allUsers = users; renderUsersTable(users); })
        .catch(function () { document.getElementById('usersTable').innerHTML = '<div class="loading-row">Loi tai du lieu</div>'; });
}

function renderUsersTable(users) {
    document.getElementById('userCount').textContent = users.length + ' tài khoản';
    if (!users.length) {
        document.getElementById('usersTable').innerHTML = '<div class="loading-row">Không tìm thấy kết quả</div>';
        return;
    }
    var roleMap = { Admin: 'b-admin', Doctor: 'b-doctor', Staff: 'b-staff', Patient: 'b-patient' };
    var roleVI  = { Admin: 'Admin', Doctor: 'Bác sĩ', Staff: 'Lễ tân', Patient: 'Bệnh nhân' };
    var html = '<table class="data-table"><thead><tr>'
        + '<th>#</th><th>Họ tên</th><th>Email</th><th>SDT</th><th>Vai trò</th><th>Trạng thái</th><th>Thao tác</th>'
        + '</tr></thead><tbody>'
        + users.map(function (u) {
            var r   = u.role || u.Role;
            var rb  = roleMap[r] || 'b-patient';
            var rv  = roleVI[r]  || r;
            var act = (u.isActive !== undefined ? u.isActive : u.IsActive);
            var ab  = act ? 'b-active">Hoạt động' : 'b-locked">Bị khóa';
            var isAdm = r === 'Admin';
            var nameSafe = (u.fullName || u.FullName || '').replace(/'/g, "\\'");
            var uid = u.id || u.Id;
            var toggleBtn = isAdm ? ''
                : '<button class="btn-sm ' + (act ? 'danger' : '') + '" title="' + (act ? 'Khóa tài khoản' : 'Mở khóa') + '" onclick="toggleUser(' + uid + ',this)" style="padding:0 8px;font-size:15px;">'
                  + (act ? '🔒' : '🔓') + '</button>';
            var resetBtn = isAdm ? '&#8212;'
                : '<button class="btn-sm" title="Đặt lại mật khẩu" onclick="openResetPw(' + uid + ',\'' + nameSafe + '\')" style="padding:0 8px;font-size:15px;">🔑</button>';
            var deleteBtn = isAdm ? ''
                : '<button class="btn-sm danger" title="Xóa tài khoản" onclick="deleteUser(' + uid + ',\'' + nameSafe + '\')" style="padding:0 8px;font-size:15px;">🗑️</button>';
            return '<tr>'
                + '<td style="color:#7a9bb0">' + uid + '</td>'
                + '<td><strong>' + (u.fullName || u.FullName) + '</strong></td>'
                + '<td>' + (u.email || u.Email) + '</td>'
                + '<td>' + (u.phone || u.Phone) + '</td>'
                + '<td><span class="badge ' + rb + '">' + rv + '</span></td>'
                + '<td><span class="badge ' + ab + '</span></td>'
                + '<td><div style="display:flex;gap:5px;flex-wrap:wrap;">' + toggleBtn + resetBtn + deleteBtn + '</div></td>'
                + '</tr>';
        }).join('')
        + '</tbody></table>';
    document.getElementById('usersTable').innerHTML = html;
}

function filterUsers() {
    var q    = document.getElementById('userSearch').value.toLowerCase();
    var role = document.getElementById('userRoleFilter') ? document.getElementById('userRoleFilter').value : '';
    var filtered = allUsers.filter(function (u) {
        var matchText = (u.fullName || u.FullName || '').toLowerCase().includes(q)
            || (u.email    || u.Email    || '').toLowerCase().includes(q)
            || (u.userName || u.UserName || '').toLowerCase().includes(q);
        var matchRole = !role || (u.role || u.Role) === role;
        return matchText && matchRole;
    });
    renderUsersTable(filtered);
}

function deleteUser(id, name) {
    if (!confirm('Bạn có chắc muốn xóa tài khoản "' + name + '"?\nHành động này không thể hoàn tác!')) return;
    var csrf = (document.querySelector('input[name="__RequestVerificationToken"]') || {}).value || '';
    fetch('/Admin/DeleteUser?id=' + id, { method: 'POST', headers: { 'RequestVerificationToken': csrf } })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (d.success || d.Success) {
                showToast('Đã xóa tài khoản thành công', 'ok');
                loadUsers();
            } else {
                showToast(d.message || d.Message || 'Xóa thất bại', 'err');
            }
        })
        .catch(function () { showToast('Lỗi kết nối server', 'err'); });
}

function toggleUser(id, btn) {
    var action = btn.textContent.trim();
    if (!confirm('Xac nhan ' + action + ' tai khoan ID=' + id + '?')) return;
    btn.disabled = true;
    fetch('/Admin/ToggleUserActive?id=' + id, { method: 'POST', headers: { 'RequestVerificationToken': csrf } })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (d.success || d.Success) { showToast('Thao tac thanh cong.', true); tabLoaded['users'] = false; loadUsers(); }
            else { showToast(d.message || d.Message || 'Lỗi.', false); btn.disabled = false; }
        })
        .catch(function () { showToast('Loi ket noi.', false); btn.disabled = false; });
}

// ══════════════════════════════════════════════════════════
// TAB 3: BAC SI
// ══════════════════════════════════════════════════════════
function loadDoctors() {
    document.getElementById('doctorsTable').innerHTML = '<div class="loading-row">Dang tai...</div>';
    fetch('/Admin/GetDoctors')
        .then(function (r) { return r.json(); })
        .then(function (docs) { doctorCache = docs; allDoctors = docs; renderDoctorsTable(docs); })
        .catch(function () { document.getElementById('doctorsTable').innerHTML = '<div class="loading-row">Loi tai du lieu</div>'; });
}

function renderDoctorsTable(docs) {
    if (!docs.length) {
        document.getElementById('doctorsTable').innerHTML = '<div class="loading-row">Khong tim thay bac si nao</div>';
        return;
    }
    var html = '<table class="data-table"><thead><tr>'
        + '<th>Họ tên</th><th>Chuyên khoa</th><th>Học vị</th><th>Email</th><th>SDT</th><th>Nhận lịch</th><th>Thao tác</th>'
        + '</tr></thead><tbody>'
        + docs.map(function (d) {
            var id    = d.id    || d.Id;
            var name  = d.fullName    || d.FullName    || '';
            var spec  = d.specialty   || d.Specialty   || '';
            var deg   = d.degree      || d.Degree      || '';
            var desc  = d.description || d.Description || '';
            var email = d.email || d.Email || '&#8212;';
            var phone = d.phone || d.Phone || '&#8212;';
            var avail = (d.isAvailable !== undefined ? d.isAvailable : d.IsAvailable);
            var ab    = avail ? 'b-on">Nhận lịch' : 'b-off">Tạm ẩn';
            var nameSafe = name.replace(/'/g, "\\'");
            var specSafe = spec.replace(/'/g, "\\'");
            var degSafe  = deg.replace(/'/g, "\\'");
            var descSafe = desc.replace(/'/g, "\\'");
            return '<tr>'
                + '<td><strong>' + name + '</strong></td>'
                + '<td>' + spec + '</td>'
                + '<td>' + (deg || '&#8212;') + '</td>'
                + '<td>' + email + '</td>'
                + '<td>' + phone + '</td>'
                + '<td><span class="badge ' + ab + '</span></td>'
                + '<td style="display:flex;gap:6px;flex-wrap:wrap;">'
                + '<button class="btn-sm" title="Sửa thông tin" onclick="openEditDoctor(' + id + ',\'' + nameSafe + '\',\'' + specSafe + '\',\'' + degSafe + '\',\'' + descSafe + '\')" style="padding:0 8px;font-size:15px;">✏️</button>'
                + '<button class="btn-sm" title="' + (avail ? 'Tạm ẩn bác sĩ' : 'Hiện lại bác sĩ') + '" onclick="toggleDoctor(' + id + ',this)" style="padding:0 8px;font-size:15px;">' + (avail ? '🙈' : '👁️') + '</button>'
                + '</td></tr>';
        }).join('')
        + '</tbody></table>';
    document.getElementById('doctorsTable').innerHTML = html;
}

function filterDoctors() {
    var q = document.getElementById('doctorSearch').value.toLowerCase();
    var filtered = allDoctors.filter(function (d) {
        return (d.fullName || d.FullName || '').toLowerCase().includes(q)
            || (d.specialty || d.Specialty || '').toLowerCase().includes(q);
    });
    renderDoctorsTable(filtered);
}

function toggleDoctor(id, btn) {
    btn.disabled = true;
    fetch('/Admin/ToggleDoctorAvailable?id=' + id, { method: 'POST', headers: { 'RequestVerificationToken': csrf } })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (d.success || d.Success) { showToast('Cập nhật thành công.', true); tabLoaded['doctors'] = false; loadDoctors(); }
            else { showToast(d.message || d.Message || 'Lỗi.', false); btn.disabled = false; }
        })
        .catch(function () { showToast('Loi ket noi.', false); btn.disabled = false; });
}

// ══════════════════════════════════════════════════════════
// TAB 4: THONG KE
// ══════════════════════════════════════════════════════════
var monthlyChart = null;

function initStatsTab() {
    var today = new Date();
    var firstOfMonth = new Date(today.getFullYear(), today.getMonth(), 1);
    document.getElementById('rptFrom').value = firstOfMonth.toISOString().split('T')[0];
    document.getElementById('rptTo').value   = today.toISOString().split('T')[0];
    loadStats();
    loadMonthlyChart();
}

function loadStats() {
    fetch('/Admin/GetStatsByDoctor')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (!data || !data.length) {
                document.getElementById('doctorStatsBars').innerHTML = '<div class="loading-row">Chưa có dữ liệu</div>';
                return;
            }
            var maxTotal = Math.max.apply(null, data.map(function (d) { return d.total || d.Total || 0; })) || 1;
            var html = data.map(function (d) {
                var total = d.total || d.Total || 0;
                var done  = d.completed || d.Completed || 0;
                var pct   = Math.round(total / maxTotal * 100);
                return '<div class="bar-row">'
                    + '<div class="bar-name">' + (d.doctorName || d.DoctorName) + '</div>'
                    + '<div class="bar-track"><div class="bar-fill" style="width:' + pct + '%"></div></div>'
                    + '<div class="bar-num"><strong>' + total + '</strong> <span style="color:#2d7a52;font-size:11px"> ✓' + done + '</span></div>'
                    + '</div>';
            }).join('');
            document.getElementById('doctorStatsBars').innerHTML = html;
        })
        .catch(function () { document.getElementById('doctorStatsBars').innerHTML = '<div class="loading-row">Lỗi tải dữ liệu</div>'; });

    fetch('/Admin/GetWeeklyStats')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            var totalAll     = data.reduce(function (s, d) { return s + (d.total     || d.Total     || 0); }, 0);
            var completedAll = data.reduce(function (s, d) { return s + (d.completed || d.Completed || 0); }, 0);
            var cancelledAll = data.reduce(function (s, d) { return s + (d.cancelled || d.Cancelled || 0); }, 0);
            var other = totalAll - completedAll - cancelledAll;
            if (statusChart) statusChart.destroy();
            var canvas = document.getElementById('statusChart');
            if (totalAll === 0) {
                canvas.parentElement.innerHTML = '<div style="display:flex;align-items:center;justify-content:center;height:220px;color:#aab8c2;font-size:13px;">Chưa có dữ liệu</div>';
                return;
            }
            statusChart = new Chart(canvas, {
                type: 'doughnut',
                data: {
                    labels: ['Hoàn thành', 'Huỷ/Vắng', 'Đang xử lý'],
                    datasets: [{ data: [completedAll, cancelledAll, other], backgroundColor: ['#2d7a52', '#c0392b', '#1a6fa8'], borderWidth: 3, borderColor: '#fff' }]
                },
                options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom', labels: { font: { size: 12 }, padding: 16 } } } }
            });
        })
        .catch(function () { showToast('Lỗi tải biểu đồ trạng thái.', false); });
}

function loadMonthlyChart() {
    fetch('/Admin/GetMonthlyStats')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            var labels    = data.map(function (d) { return d.label     || d.Label     || ''; });
            var totals    = data.map(function (d) { return d.total     || d.Total     || 0; });
            var completed = data.map(function (d) { return d.completed || d.Completed || 0; });
            var cancelled = data.map(function (d) { return d.cancelled || d.Cancelled || 0; });
            if (monthlyChart) monthlyChart.destroy();
            var canvas = document.getElementById('monthlyChart');
            var hasData = totals.some(function(v) { return v > 0; });
            if (!hasData) {
                canvas.parentElement.innerHTML = '<div style="display:flex;align-items:center;justify-content:center;height:260px;color:#aab8c2;font-size:13px;">Chưa có dữ liệu trong 12 tháng qua</div>';
                return;
            }
            monthlyChart = new Chart(canvas, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [
                        { label: 'Tổng lịch',  data: totals,    backgroundColor: 'rgba(10,77,124,.18)', borderColor: '#0a4d7c', borderWidth: 2, borderRadius: 4 },
                        { label: 'Hoàn thành', data: completed, backgroundColor: 'rgba(45,122,82,.65)',  borderColor: '#2d7a52', borderWidth: 0, borderRadius: 4 },
                        { label: 'Huỷ/Vắng',  data: cancelled, backgroundColor: 'rgba(192,57,43,.55)',  borderColor: '#c0392b', borderWidth: 0, borderRadius: 4 }
                    ]
                },
                options: {
                    responsive: true, maintainAspectRatio: false,
                    plugins: { legend: { position: 'bottom', labels: { font: { size: 12 }, padding: 16 } } },
                    scales: { x: { grid: { display: false } }, y: { beginAtZero: true, grid: { color: '#f0f4f8' } } }
                }
            });
        })
        .catch(function () { showToast('Lỗi tải biểu đồ tháng.', false); });
}

function loadReport() {
    var from = document.getElementById('rptFrom').value;
    var to   = document.getElementById('rptTo').value;
    if (!from || !to) { showToast('Vui long chon khoang thoi gian.', false); return; }
    if (from > to)    { showToast('Ngay bat dau phai truoc ngay ket thuc.', false); return; }

    fetch('/Admin/GetReportStats?from=' + from + '&to=' + to)
        .then(function (r) { return r.json(); })
        .then(function (d) {
            document.getElementById('rptTotal').textContent     = d.total     || d.Total     || 0;
            document.getElementById('rptCompleted').textContent = d.completed || d.Completed || 0;
            document.getElementById('rptCancelled').textContent = d.cancelled || d.Cancelled || 0;
            document.getElementById('rptPending').textContent   = d.pending   || d.Pending   || 0;
            document.getElementById('rptCompRate').textContent  = (d.completionRate   || d.CompletionRate   || 0) + '%';
            document.getElementById('rptCancRate').textContent  = (d.cancellationRate || d.CancellationRate || 0) + '%';
            document.getElementById('rptKpi').style.display   = 'grid';
            document.getElementById('rptRates').style.display = 'flex';
            var byDoctor = d.byDoctor || d.ByDoctor || [];
            if (byDoctor.length) {
                var html = '<table class="data-table"><thead><tr>'
                    + '<th>Bác sĩ</th><th>Tổng lịch</th><th>Hoàn thành</th><th>Hủy/Vắng</th><th>Tỷ lệ HT</th>'
                    + '</tr></thead><tbody>'
                    + byDoctor.map(function (doc) {
                        var total = doc.total || doc.Total || 0;
                        var done  = doc.completed || doc.Completed || 0;
                        var canc  = doc.cancelled || doc.Cancelled || 0;
                        var rate  = total > 0 ? Math.round(done / total * 100) : 0;
                        return '<tr>'
                            + '<td><strong>' + (doc.doctor || doc.Doctor) + '</strong></td>'
                            + '<td>' + total + '</td>'
                            + '<td><span style="color:#2d7a52;font-weight:600;">' + done + '</span></td>'
                            + '<td><span style="color:#c0392b;font-weight:600;">' + canc + '</span></td>'
                            + '<td><strong>' + rate + '%</strong></td>'
                            + '</tr>';
                    }).join('')
                    + '</tbody></table>';
                document.getElementById('rptDoctorBody').innerHTML = html;
                document.getElementById('rptDoctorTable').style.display = 'block';
            }
            showToast('Da tai bao cao.', true);
        })
        .catch(function () { showToast('Loi tai bao cao.', false); });
}

function printReport() {
    var from = document.getElementById('rptFrom').value || '&#8212;';
    var to   = document.getElementById('rptTo').value   || '&#8212;';
    var total     = document.getElementById('rptTotal').textContent     || '&#8212;';
    var completed = document.getElementById('rptCompleted').textContent || '&#8212;';
    var cancelled = document.getElementById('rptCancelled').textContent || '&#8212;';
    var pending   = document.getElementById('rptPending').textContent   || '&#8212;';
    var compRate  = document.getElementById('rptCompRate').textContent  || '&#8212;';
    var cancRate  = document.getElementById('rptCancRate').textContent  || '&#8212;';
    var tableHtml = document.getElementById('rptDoctorBody') ? document.getElementById('rptDoctorBody').innerHTML : '';
    var win = window.open('', '_blank', 'width=900,height=700');
    win.document.write('<html><head><title>Bao cao lich kham</title>'
        + '<style>body{font-family:Arial,sans-serif;padding:32px;color:#0d2d44;}'
        + '.header{text-align:center;border-bottom:2px solid #0a4d7c;padding-bottom:16px;margin-bottom:24px;}'
        + 'h1{color:#0a4d7c;font-size:20px;margin:8px 0 4px;}'
        + '.kpi-row{display:flex;gap:16px;margin-bottom:24px;flex-wrap:wrap;}'
        + '.kpi{flex:1;min-width:120px;border:1px solid #d4e8f5;border-radius:10px;padding:14px;text-align:center;}'
        + '.kpi-val{font-size:28px;font-weight:700;color:#0a4d7c;}'
        + 'table{width:100%;border-collapse:collapse;font-size:13px;}'
        + 'th{background:#f0f8ff;padding:9px 12px;text-align:left;border-bottom:1px solid #d4e8f5;}'
        + 'td{padding:9px 12px;border-bottom:1px solid #f0f4f8;}'
        + '</style></head><body>'
        + '<div class="header"><div>PHONG KHAM TAI MUI HONG</div><h1>BAO CAO THONG KE LICH KHAM</h1><div>Tu ngay ' + from + ' den ngay ' + to + '</div></div>'
        + '<div class="kpi-row">'
        + '<div class="kpi"><div class="kpi-val">' + total + '</div><div>Tong lich kham</div></div>'
        + '<div class="kpi"><div class="kpi-val" style="color:#2d7a52">' + completed + '</div><div>Hoan thanh</div></div>'
        + '<div class="kpi"><div class="kpi-val" style="color:#c0392b">' + cancelled + '</div><div>Huy / Vang</div></div>'
        + '<div class="kpi"><div class="kpi-val" style="color:#c9a227">' + pending   + '</div><div>Dang xu ly</div></div>'
        + '</div>'
        + (tableHtml ? '<h3>Chi tiết theo bác sĩ</h3><table><thead><tr><th>Bác sĩ</th><th>Tổng lịch</th><th>Hoàn thành</th><th>Hủy/Vắng</th><th>Tỷ lệ HT</th></tr></thead><tbody>' + tableHtml + '</tbody></table>' : '')
        + '<div style="margin-top:32px;font-size:11px;color:#aaa;text-align:right;">In ngay: ' + new Date().toLocaleDateString('vi-VN') + '</div>'
        + '</body></html>');
    win.document.close();
    setTimeout(function () { win.print(); }, 500);
}

// ══════════════════════════════════════════════════════════
// TAB 5: LICH LAM VIEC
// ══════════════════════════════════════════════════════════
var selDoctors  = new Set();
var selWeekdays = new Set([1, 2, 3, 4, 5]);
var shifts      = [];
var shiftCnt    = 0;

function initScheduleTab() {
    var today = new Date();
    var mon   = new Date(today); mon.setDate(today.getDate() - (today.getDay() || 7) + 1);
    var sun   = new Date(mon);   sun.setDate(mon.getDate() + 6);
    document.getElementById('schFrom').value = mon.toISOString().split('T')[0];
    document.getElementById('schTo').value   = sun.toISOString().split('T')[0];
    if (doctorCache.length) { populateDoctorFilter(); loadSchedules(); }
    else {
        fetch('/Admin/GetDoctors').then(function (r) { return r.json(); })
            .then(function (docs) { doctorCache = docs; populateDoctorFilter(); loadSchedules(); });
    }
}

function populateDoctorFilter() {
    var sel = document.getElementById('schDoctor');
    sel.innerHTML = '<option value="">Tat ca bac si</option>';
    doctorCache.forEach(function (d) { sel.appendChild(new Option(d.fullName || d.FullName, d.id || d.Id)); });
}

function loadSchedules() {
    var doctorId = document.getElementById('schDoctor').value;
    var from     = document.getElementById('schFrom').value;
    var to       = document.getElementById('schTo').value;
    var statusEl = document.getElementById('schStatus');
    var statusFilter = statusEl ? statusEl.value : '';
    var url = '/Admin/GetSchedules?';
    if (doctorId) url += 'doctorId=' + doctorId + '&';
    if (from)     url += 'from=' + from + '&';
    if (to)       url += 'to=' + to + '&';
    if (statusFilter) url += 'status=' + statusFilter + '&';
    document.getElementById('scheduleTable').innerHTML = '<div class="loading-row">Dang tai...</div>';
    fetch(url.replace(/[?&]$/, ''))
        .then(function (r) { return r.json(); })
        .then(function (data) {
            // Client-side status filter fallback (nếu API không hỗ trợ param status)
            if (statusFilter && Array.isArray(data)) {
                data = data.filter(function (s) {
                    var st = (s.status || s.Status || 'ChoDuyet');
                    if (statusFilter === 'ChoDuyet') return st === 'ChoDuyet' || (st !== 'DaDuyet' && st !== 'DaHuy' && st !== 'Approved');
                    if (statusFilter === 'DaDuyet')  return st === 'DaDuyet' || st === 'Approved';
                    if (statusFilter === 'DaHuy')    return st === 'DaHuy';
                    return true;
                });
            }
            if (!Array.isArray(data) || !data.length) {
                document.getElementById('scheduleTable').innerHTML = '<div class="loading-row">Không có lịch làm việc trong khoảng thời gian này</div>';
                return;
            }
            var html = '<table class="data-table"><thead><tr>'
                + '<th>Bác sĩ</th><th>Ngày</th><th>Ca làm việc</th><th>Đã đặt / Tối đa</th><th>Còn trống</th><th>Trạng thái</th><th>Thao tác</th>'
                + '</tr></thead><tbody>'
                + data.map(function (s) {
                    var cur    = s.currentPatients || s.CurrentPatients || 0;
                    var max    = s.maxPatients     || s.MaxPatients     || 1;
                    var rem    = s.remainingSlots  || s.RemainingSlots  || (max - cur);
                    var pct    = Math.round(cur / max * 100);
                    var col    = pct >= 100 ? '#c0392b' : pct >= 70 ? '#c9a227' : '#2d7a52';
                    var bar    = '<div style="background:#e8f0f7;border-radius:5px;height:8px;width:90px;display:inline-block;vertical-align:middle;">'
                               + '<div style="background:' + col + ';height:8px;border-radius:5px;width:' + Math.min(pct, 100) + '%;"></div></div>';
                    var wdate  = new Date(s.workDate || s.WorkDate);
                    var dateStr = wdate.toLocaleDateString('vi-VN', { weekday: 'short', day: '2-digit', month: '2-digit', year: 'numeric' });
                    var status = s.status || s.Status || 'ChoDuyet';
                    var statusBadge, isPending;
                    if (status === 'DaDuyet' || status === 'Approved') {
                        statusBadge = '<span class="badge b-active">✓ Đã duyệt</span>';
                        isPending = false;
                    } else if (status === 'DaHuy') {
                        statusBadge = '<span class="badge b-locked">✗ Đã hủy</span>';
                        isPending = false;
                    } else {
                        statusBadge = '<span class="badge b-pending">⏳ Chờ duyệt</span>';
                        isPending = true;
                    }
                    var sid    = s.id || s.Id;
                    var canDel = cur === 0;
                    var approveBtn = isPending
                        ? '<button class="btn-sm approve" onclick="approveSchedule(' + sid + ')">✓ Duyệt</button>'
                        : '';
                    return '<tr>'
                        + '<td><strong>' + (s.doctorName || s.DoctorName) + '</strong></td>'
                        + '<td>' + dateStr + '</td>'
                        + '<td>' + (s.startTime || s.StartTime) + ' – ' + (s.endTime || s.EndTime) + '</td>'
                        + '<td>' + bar + ' <span style="font-size:12px;color:#7a9bb0;margin-left:8px;">' + cur + ' / ' + max + '</span></td>'
                        + '<td><strong style="color:' + col + '">' + rem + '</strong></td>'
                        + '<td>' + statusBadge + '</td>'
                        + '<td><div style="display:flex;gap:5px;flex-wrap:wrap;">'
                        + approveBtn
                        + '<button class="btn-sm danger" onclick="deleteSchedule(' + sid + ')" '
                        + (canDel ? '' : 'disabled title="Đã có bệnh nhân đặt"') + '>Xóa</button>'
                        + '</div></td>'
                        + '</tr>';
                }).join('')
                + '</tbody></table>';
            document.getElementById('scheduleTable').innerHTML = html;
        })
        .catch(function () { document.getElementById('scheduleTable').innerHTML = '<div class="loading-row">Lỗi tải dữ liệu</div>'; });
}

function approveSchedule(id) {
    adminConfirm({
        icon: '✅', title: 'Duyệt lịch làm việc',
        msg: 'Xác nhận duyệt lịch làm việc này?',
        okLabel: '✓ Duyệt', okColor: '#2d7a52',
        onOk: function () {
            fetch('/Admin/ApproveSchedule?id=' + id, { method: 'POST', headers: { 'RequestVerificationToken': csrf } })
                .then(function (r) { return r.json(); })
                .then(function (d) {
                    if (d.success || d.Success) { showToast('Đã duyệt lịch làm việc.', true); loadSchedules(); }
                    else showToast(d.message || d.Message || 'Không thể duyệt.', false);
                })
                .catch(function () { showToast('Lỗi kết nối.', false); });
        }
    });
}

function rejectSchedule(id) {
    adminConfirm({
        icon: '❌', title: 'Từ chối lịch làm việc',
        msg: 'Xác nhận từ chối lịch làm việc này? Bác sĩ sẽ cần tạo lại lịch mới.',
        okLabel: '✗ Từ chối', okColor: '#c0392b',
        onOk: function () {
            fetch('/Admin/RejectSchedule?id=' + id, { method: 'POST', headers: { 'RequestVerificationToken': csrf } })
                .then(function (r) { return r.json(); })
                .then(function (d) {
                    if (d.success || d.Success) { showToast('Đã từ chối lịch làm việc.', true); loadSchedules(); }
                    else showToast(d.message || d.Message || 'Không thể từ chối.', false);
                })
                .catch(function () { showToast('Lỗi kết nối.', false); });
        }
    });
}

function approveAllSchedules() {
    var pendingCount = document.querySelectorAll('#scheduleTable .badge.b-pending').length;
    if (pendingCount === 0) { showToast('Không có lịch nào đang chờ duyệt.', false); return; }
    adminConfirm({
        icon: '✅', title: 'Duyệt tất cả lịch chờ',
        msg: 'Xác nhận duyệt tất cả ' + pendingCount + ' lịch đang chờ duyệt trong khoảng thời gian này?',
        okLabel: '✓ Duyệt tất cả', okColor: '#2d7a52',
        onOk: function () { _doApproveAll(); }
    });
}
function _doApproveAll() {
    var doctorId = document.getElementById('schDoctor').value;
    var from     = document.getElementById('schFrom').value;
    var to       = document.getElementById('schTo').value;
    var url = '/Admin/ApproveAllSchedules?';
    if (doctorId) url += 'doctorId=' + doctorId + '&';
    if (from)     url += 'from=' + from + '&';
    if (to)       url += 'to=' + to + '&';
    fetch(url.replace(/[?&]$/, ''), { method: 'POST', headers: { 'RequestVerificationToken': csrf } })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (d.success || d.Success) { showToast(d.message || d.Message || 'Đã duyệt tất cả.', true); loadSchedules(); }
            else showToast(d.message || d.Message || 'Lỗi.', false);
        })
        .catch(function () { showToast('Lỗi kết nối.', false); });
}

function openAddSchedule() {
    selDoctors.clear(); shifts = []; shiftCnt = 0;
    document.getElementById('schErr').style.display = 'none';
    document.getElementById('schOk').style.display  = 'none';
    document.getElementById('batchPreview').style.display = 'none';
    var d = new Date(); d.setDate(d.getDate() + (8 - d.getDay()) % 7 || 7);
    var e = new Date(d); e.setDate(d.getDate() + 6);
    document.getElementById('schMFrom').value = d.toISOString().split('T')[0];
    document.getElementById('schMTo').value   = e.toISOString().split('T')[0];
    document.getElementById('schDoctorList').innerHTML = doctorCache.map(function (doc) {
        var id = doc.id || doc.Id;
        return '<div class="chip" id="dc-' + id + '" onclick="toggleDocChip(' + id + ')">' + (doc.fullName || doc.FullName) + '</div>';
    }).join('');
    selWeekdays = new Set([1, 2, 3, 4, 5]);
    renderWDChips();
    addShift('07:00', '11:00');
    addShift('13:00', '17:00');
    document.getElementById('schModal').classList.add('open');
    updatePreview();
}
function closeAddSchedule() { document.getElementById('schModal').classList.remove('open'); }

function renderWDChips() {
    var days = [{ v: 1, l: 'Thu 2' }, { v: 2, l: 'Thu 3' }, { v: 3, l: 'Thu 4' }, { v: 4, l: 'Thu 5' }, { v: 5, l: 'Thu 6' }, { v: 6, l: 'Thu 7' }, { v: 0, l: 'CN' }];
    document.getElementById('wdContainer').innerHTML = days.map(function (d) {
        return '<div class="wd-chip ' + (selWeekdays.has(d.v) ? 'selected' : '') + '" id="wd-' + d.v + '" onclick="toggleWD(' + d.v + ')">' + d.l + '</div>';
    }).join('');
}
function toggleDocChip(id) {
    if (selDoctors.has(id)) selDoctors.delete(id); else selDoctors.add(id);
    var el = document.getElementById('dc-' + id);
    if (el) el.className = 'chip' + (selDoctors.has(id) ? ' selected' : '');
    updatePreview();
}
function toggleWD(v) {
    if (selWeekdays.has(v)) selWeekdays.delete(v); else selWeekdays.add(v);
    var el = document.getElementById('wd-' + v);
    if (el) el.className = 'wd-chip' + (selWeekdays.has(v) ? ' selected' : '');
    updatePreview();
}
function selectWD(arr) { selWeekdays = new Set(arr); renderWDChips(); updatePreview(); }
function addShift(s, e) { var id = ++shiftCnt; shifts.push({ id: id, start: s || '07:00', end: e || '11:00' }); renderShifts(); updatePreview(); }
function removeShift(id) { shifts = shifts.filter(function (s) { return s.id !== id; }); renderShifts(); updatePreview(); }
function renderShifts() {
    if (!shifts.length) { document.getElementById('shiftList').innerHTML = '<div style="font-size:12px;color:#7a9bb0;padding:8px 0">Chua co ca nao.</div>'; return; }
    document.getElementById('shiftList').innerHTML = shifts.map(function (s) {
        return '<div class="shift-row" id="sr-' + s.id + '">'
            + '<input type="time" class="time-inp" value="' + s.start + '" onchange="updShift(' + s.id + ',\'start\',this.value)" />'
            + '<span style="color:#7a9bb0;font-size:13px">-</span>'
            + '<input type="time" class="time-inp" value="' + s.end + '" onchange="updShift(' + s.id + ',\'end\',this.value)" />'
            + '<button onclick="removeShift(' + s.id + ')" style="width:30px;height:30px;border-radius:6px;border:1px solid #f5c6c6;background:#fdecea;color:#c0392b;cursor:pointer;font-size:16px;flex-shrink:0">x</button>'
            + '</div>';
    }).join('');
}
function updShift(id, field, val) { var s = shifts.find(function (x) { return x.id === id; }); if (s) s[field] = val; updatePreview(); }
function updatePreview() {
    var from = document.getElementById('schMFrom').value;
    var to   = document.getElementById('schMTo').value;
    var el   = document.getElementById('batchPreview');
    if (!from || !to || !selDoctors.size || !shifts.length || !selWeekdays.size) { el.style.display = 'none'; return; }
    var days = 0, d = new Date(from), tod = new Date(to);
    while (d <= tod) { if (selWeekdays.has(d.getDay())) days++; d.setDate(d.getDate() + 1); }
    var total = selDoctors.size * days * shifts.length;
    el.style.display = 'block';
    el.innerHTML = 'Se tao toi da <strong>' + total + ' lich</strong> - ' + selDoctors.size + ' bac si x ' + days + ' ngay x ' + shifts.length + ' ca.';
}
function saveScheduleBatch() {
    var from = document.getElementById('schMFrom').value;
    var to   = document.getElementById('schMTo').value;
    var max  = parseInt(document.getElementById('schMMax').value) || 10;
    var errEl = document.getElementById('schErr');
    errEl.style.display = 'none';
    if (!selDoctors.size)  { errEl.textContent = 'Vui long chon it nhat 1 bac si.'; errEl.style.display = 'block'; return; }
    if (!from || !to)      { errEl.textContent = 'Vui long chon khoang ngay.';       errEl.style.display = 'block'; return; }
    if (!selWeekdays.size) { errEl.textContent = 'Vui long chon it nhat 1 thu.';     errEl.style.display = 'block'; return; }
    if (!shifts.length)    { errEl.textContent = 'Vui long them it nhat 1 ca.';      errEl.style.display = 'block'; return; }
    for (var i = 0; i < shifts.length; i++) {
        if (shifts[i].start >= shifts[i].end) { errEl.textContent = 'Ca ' + (i + 1) + ': gio ket thuc phai sau gio bat dau.'; errEl.style.display = 'block'; return; }
    }
    var btn = document.getElementById('schMSave');
    btn.disabled = true; btn.textContent = 'Dang tao...';
    fetch('/Admin/CreateScheduleBatch', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrf },
        body: JSON.stringify({ doctorIds: Array.from(selDoctors), fromDate: from, toDate: to, weekdays: Array.from(selWeekdays), maxPatients: max, shifts: shifts.map(function (s) { return { startTime: s.start + ':00', endTime: s.end + ':00' }; }) })
    })
    .then(function (r) { return r.json(); })
    .then(function (d) {
        btn.disabled = false; btn.textContent = 'Tao lich hang loat';
        if (d.success || d.Success) {
            document.getElementById('schOk').textContent = d.message || d.Message;
            document.getElementById('schOk').style.display = 'block';
            showToast(d.message || d.Message, true);
            setTimeout(function () { closeAddSchedule(); loadSchedules(); }, 2000);
        } else { errEl.textContent = d.message || d.Message || 'Co loi xay ra.'; errEl.style.display = 'block'; }
    })
    .catch(function () { btn.disabled = false; btn.textContent = 'Tao lich hang loat'; errEl.textContent = 'Loi ket noi.'; errEl.style.display = 'block'; });
}
function deleteSchedule(id) {
    adminConfirm({
        icon: '🗑️', title: 'Xóa lịch làm việc',
        msg: 'Bạn có chắc muốn xóa lịch làm việc này? Hành động không thể hoàn tác.',
        okLabel: 'Xóa lịch', okColor: '#c0392b',
        onOk: function () {
            fetch('/Admin/DeleteSchedule?id=' + id, { method: 'POST', headers: { 'RequestVerificationToken': csrf } })
                .then(function (r) { return r.json(); })
                .then(function (d) {
                    if (d.success || d.Success) { showToast('Đã xóa lịch làm việc.', true); loadSchedules(); }
                    else showToast(d.message || d.Message || 'Không thể xóa. Lịch đang có bệnh nhân đặt.', false);
                })
                .catch(function () { showToast('Lỗi kết nối.', false); });
        }
    });
}

// ══════════════════════════════════════════════════════════
// TAB 6: BAI VIET
// ══════════════════════════════════════════════════════════
var articleCache = {};   // id → full article object

function loadAllArticles() {
    document.getElementById('articlesTable').innerHTML = '<div class="loading-row">Đang tải...</div>';
    fetch('/Admin/GetAllArticles')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (!data.length) { document.getElementById('articlesTable').innerHTML = '<div class="loading-row">Chưa có bài viết nào.</div>'; return; }
            var statusFilter = document.getElementById('artFilterStatus') ? document.getElementById('artFilterStatus').value : '';
            if (statusFilter) {
                data = data.filter(function (a) {
                    var sd = (a.statusDisplay || a.StatusDisplay || '').toLowerCase();
                    if (statusFilter === 'pub')     return sd === 'đã đăng';
                    if (statusFilter === 'pending') return sd === 'chờ duyệt';
                    if (statusFilter === 'draft')   return sd === 'nháp';
                    return true;
                });
            }
            var html = '<table class="data-table"><thead><tr>'
                + '<th>Tiêu đề</th><th>Tác giả</th><th>Chủ đề</th><th>Trạng thái</th><th>Ngày tạo</th><th>Thao tác</th>'
                + '</tr></thead><tbody>'
                + data.map(function (a) {
                    var id    = a.id    || a.Id;
                    var title = a.title || a.Title || '';
                    var isPub = (a.statusDisplay || a.StatusDisplay || '') === 'Đã đăng';
                    articleCache[id] = a;   // lưu toàn bộ object, không escape gì cả
                    return '<tr>'
                        + '<td><strong>' + title + '</strong></td>'
                        + '<td>' + (a.authorName || a.AuthorName || '&#8212;') + '</td>'
                        + '<td>' + (a.category   || a.Category  || '&#8212;') + '</td>'
                        + '<td><span class="badge ' + (isPub ? 'b-pub">Đã đăng' : 'b-draft">Nháp') + '</span></td>'
                        + '<td>' + new Date(a.createdAt || a.CreatedAt).toLocaleDateString('vi-VN') + '</td>'
                        + '<td style="display:flex;gap:6px;">'
                        + '<button class="btn-sm" title="Sửa bài viết" onclick="openAdminEditor(' + id + ')" style="padding:0 8px;font-size:15px;">✏️</button>'
                        + '<button class="btn-sm" title="' + (isPub ? 'Gỡ bài' : 'Duyệt đăng') + '" onclick="toggleArticle(' + id + ',this)" style="padding:0 8px;font-size:15px;">' + (isPub ? '📤' : '✅') + '</button>'
                        + '</td></tr>';
                }).join('')
                + '</tbody></table>';
            document.getElementById('articlesTable').innerHTML = html;
        })
        .catch(function () { document.getElementById('articlesTable').innerHTML = '<div class="loading-row">Lỗi tải dữ liệu.</div>'; });
}

function toggleArticle(id, btn) {
    btn.disabled = true;
    fetch('/Admin/ToggleArticleStatus?id=' + id, { method: 'POST', headers: { 'RequestVerificationToken': csrf } })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (d.success || d.Success) { showToast('Cập nhật thành công.', true); tabLoaded['articles'] = false; loadAllArticles(); }
            else { showToast(d.message || d.Message || 'Lỗi.', false); btn.disabled = false; }
        });
}

// ── ADMIN ARTICLE EDITOR ──────────────────────────────────
var _admEditId = null;

function openAdminEditor(idOrNull) {
    _admEditId = null;
    var article = idOrNull ? (articleCache[idOrNull] || null) : null;

    document.getElementById('admEditorTitle').textContent  = article ? 'Sửa bài viết' : 'Viết bài mới';
    document.getElementById('admArtTitle').value           = article ? (article.title    || article.Title    || '') : '';
    document.getElementById('admArtSummary').value         = article ? (article.summary  || article.Summary  || '') : '';
    document.getElementById('admArtContent').value         = article ? (article.content  || article.Content  || '') : '';
    document.getElementById('admArtCategory').value        = article ? (article.category || article.Category || '') : '';
    document.getElementById('admArtThumbnail').value       = article ? (article.thumbnailUrl || article.ThumbnailUrl || '') : '';
    document.getElementById('admEditorErr').style.display  = 'none';
    document.getElementById('admEditorOk').style.display   = 'none';

    var thumb = article ? (article.thumbnailUrl || article.ThumbnailUrl || '') : '';
    if (thumb) {
        document.getElementById('admThumbPreview').src              = thumb;
        document.getElementById('admThumbPreview').style.display    = '';
        document.getElementById('admThumbPlaceholder').style.display = 'none';
    } else {
        document.getElementById('admThumbPreview').style.display    = 'none';
        document.getElementById('admThumbPlaceholder').style.display = '';
    }

    // Ẩn phần chọn tác giả — Admin tạo bài không cần chọn
    var doctorGroup = document.getElementById('admArtDoctorId');
    if (doctorGroup && doctorGroup.closest) {
        var grp = doctorGroup.closest('.adm-editor-group');
        if (grp) grp.style.display = 'none';
    }

    var targetDoctorId = article ? (article.doctorId || article.DoctorId || '') : '';
    var sel = document.getElementById('admArtDoctorId');

    function setDoctor() { if (targetDoctorId) sel.value = targetDoctorId; }

    if (sel.options.length <= 1) {
        sel.innerHTML = '<option value="">Đang tải...</option>';
        fetch('/Admin/GetDoctors')
            .then(function (r) { return r.json(); })
            .then(function (docs) {
                sel.innerHTML = '<option value="">-- Chọn bác sĩ --</option>'
                    + docs.map(function (d) {
                        return '<option value="' + (d.id || d.Id) + '">' + (d.fullName || d.FullName) + '</option>';
                    }).join('');
                setDoctor();
            });
    } else {
        setDoctor();
    }

    if (article) _admEditId = article.id || article.Id;
    document.getElementById('admEditorOverlay').style.display = 'flex';
}

function closeAdminEditor() {
    document.getElementById('admEditorOverlay').style.display = 'none';
}

function admPreviewUpload(input) {
    var file = input.files[0];
    if (!file) return;
    if (file.size > 5 * 1024 * 1024) {
        document.getElementById('admUploadErr').textContent = 'Ảnh không được vượt quá 5MB';
        document.getElementById('admUploadErr').style.display = '';
        return;
    }
    document.getElementById('admUploadErr').style.display = 'none';
    var fd = new FormData();
    fd.append('file', file);
    fetch('/Admin/UploadImage', { method: 'POST', body: fd, headers: { 'RequestVerificationToken': csrf } })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            var url = d.url || d.Url || '';
            if (url) {
                document.getElementById('admArtThumbnail').value         = url;
                document.getElementById('admThumbPreview').src           = url;
                document.getElementById('admThumbPreview').style.display = '';
                document.getElementById('admThumbPlaceholder').style.display = 'none';
            } else {
                document.getElementById('admUploadErr').textContent = d.message || 'Upload thất bại';
                document.getElementById('admUploadErr').style.display = '';
            }
        })
        .catch(function () {
            document.getElementById('admUploadErr').textContent = 'Lỗi kết nối khi upload ảnh';
            document.getElementById('admUploadErr').style.display = '';
        });
}

function saveAdminArticle(publish) {
    var title    = document.getElementById('admArtTitle').value.trim();
    var summary  = document.getElementById('admArtSummary').value.trim();
    var content  = document.getElementById('admArtContent').value.trim();
    var category = document.getElementById('admArtCategory').value;
    var doctorId = document.getElementById('admArtDoctorId').value;
    var thumb    = document.getElementById('admArtThumbnail').value;

    var errEl = document.getElementById('admEditorErr');
    errEl.style.display = 'none';
    if (!title)    { errEl.textContent = 'Vui lòng nhập tiêu đề bài viết.';  errEl.style.display = ''; return; }
    if (!category) { errEl.textContent = 'Vui lòng chọn chủ đề.';            errEl.style.display = ''; return; }
    if (!content)  { errEl.textContent = 'Vui lòng nhập nội dung bài viết.'; errEl.style.display = ''; return; }

    var payload = {
        title:     title,
        summary:   summary,
        content:   content,
        category:  category,
        doctorId:  null,   // Admin tạo bài — API tự chọn bác sĩ placeholder
        thumbnail: thumb,
        publish:   publish
    };
    var url = _admEditId ? '/Admin/AdminUpdateArticle?id=' + _admEditId : '/Admin/AdminCreateArticle';

    fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrf },
        body: JSON.stringify(payload)
    })
    .then(function (r) { return r.json(); })
    .then(function (d) {
        if (d.success || d.Success) {
            showToast(publish ? 'Đã đăng bài viết!' : 'Đã lưu nháp.', true);
            closeAdminEditor();
            tabLoaded['articles'] = false;
            loadAllArticles();
        } else {
            errEl.textContent = d.message || d.Message || 'Lưu thất bại.';
            errEl.style.display = '';
        }
    })
    .catch(function () { errEl.textContent = 'Lỗi kết nối server.'; errEl.style.display = ''; });
}

// ══════════════════════════════════════════════════════════
// MODAL: Tao tai khoan Staff / Doctor
// ══════════════════════════════════════════════════════════
function openAddUser(role) {
    document.getElementById('auRole').value = role;
    document.getElementById('addUserTitle').textContent = role === 'Doctor' ? 'Thêm bác sĩ' : 'Thêm lễ tân';
    document.getElementById('addUserSub').textContent   = role === 'Doctor' ? 'Tạo tài khoản bác sĩ' : 'Tạo tài khoản nhân viên lễ tân';
    document.getElementById('auDoctorFields').style.display = role === 'Doctor' ? 'block' : 'none';
    ['auErr', 'auOk'].forEach(function (id) { document.getElementById(id).style.display = 'none'; });
    ['auHoTenDem', 'auTen', 'auPhone', 'auEmail', 'auPassword', 'auSpecialty', 'auDegree', 'auDescription']
        .forEach(function (id) { var el = document.getElementById(id); if (el) el.value = id === 'auSpecialty' ? 'Tai Mui Hong' : ''; });
    document.getElementById('addUserModal').classList.add('open');
}
function closeAddUser() { document.getElementById('addUserModal').classList.remove('open'); }

function saveAddUser() {
    var role     = document.getElementById('auRole').value;
    var hoTenDem = document.getElementById('auHoTenDem').value.trim();
    var ten      = document.getElementById('auTen').value.trim();
    var phone    = document.getElementById('auPhone').value.trim();
    var email    = document.getElementById('auEmail').value.trim();
    var password = document.getElementById('auPassword').value;
    var errEl = document.getElementById('auErr');
    var okEl  = document.getElementById('auOk');
    errEl.style.display = 'none'; okEl.style.display = 'none';
    if (!hoTenDem || !ten || !phone || !email || !password) { errEl.textContent = 'Vui long dien day du thong tin.'; errEl.style.display = 'block'; return; }
    if (password.length < 8) { errEl.textContent = 'Mật khẩu phải ít nhất 8 ký tự.'; errEl.style.display = 'block'; return; }
    var payload = { hoTenDem: hoTenDem, ten: ten, phone: phone, email: email, password: password, role: role,
        specialty:   role === 'Doctor' ? (document.getElementById('auSpecialty').value.trim()   || 'Tai Mui Hong') : null,
        degree:      role === 'Doctor' ? (document.getElementById('auDegree').value.trim()       || null) : null,
        description: role === 'Doctor' ? (document.getElementById('auDescription').value.trim() || null) : null
    };
    var btn = document.getElementById('auSaveBtn');
    btn.disabled = true; btn.textContent = 'Dang tao...';
    fetch('/Admin/CreateUser', { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrf }, body: JSON.stringify(payload) })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (d.success || d.Success) {
                okEl.textContent = d.message || d.Message || 'Tao tai khoan thanh cong.'; okEl.style.display = 'block';
                showToast('Tao tai khoan thanh cong!', true);
                tabLoaded['users'] = false; tabLoaded['doctors'] = false;
                setTimeout(function () { closeAddUser(); if (role === 'Doctor') loadDoctors(); else loadUsers(); }, 1500);
            } else { errEl.textContent = d.message || d.Message || 'Co loi xay ra.'; errEl.style.display = 'block'; }
        })
        .catch(function () { errEl.textContent = 'Loi ket noi.'; errEl.style.display = 'block'; })
        .finally(function () { btn.disabled = false; btn.textContent = 'Tao tai khoan'; });
}

// ══════════════════════════════════════════════════════════
// MODAL: Đặt lại mật khẩu
// ══════════════════════════════════════════════════════════
function openResetPw(userId, name) {
    document.getElementById('rpUserId').value = userId;
    document.getElementById('resetPwSub').textContent = 'Đặt lại mật khẩu cho: ' + name;
    document.getElementById('rpNewPw').value = '';
    ['rpErr', 'rpOk'].forEach(function (id) { document.getElementById(id).style.display = 'none'; });
    document.getElementById('resetPwModal').classList.add('open');
}
function closeResetPw() { document.getElementById('resetPwModal').classList.remove('open'); }
function saveResetPw() {
    var id = document.getElementById('rpUserId').value;
    var pw = document.getElementById('rpNewPw').value;
    var errEl = document.getElementById('rpErr'); var okEl = document.getElementById('rpOk');
    errEl.style.display = 'none'; okEl.style.display = 'none';
    if (!pw || pw.length < 8) { errEl.textContent = 'Mật khẩu phải ít nhất 8 ký tự.'; errEl.style.display = 'block'; return; }
    var btn = document.getElementById('rpSaveBtn'); btn.disabled = true; btn.textContent = 'Dang luu...';
    fetch('/Admin/ResetPassword?id=' + id, { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrf }, body: JSON.stringify({ newPassword: pw }) })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (d.success || d.Success) { okEl.textContent = 'Đã đặt lại mật khẩu.'; okEl.style.display = 'block'; showToast('Đặt lại mật khẩu thành công!', true); setTimeout(closeResetPw, 1500); }
            else { errEl.textContent = d.message || d.Message || 'Lỗi.'; errEl.style.display = 'block'; }
        })
        .catch(function () { errEl.textContent = 'Loi ket noi.'; errEl.style.display = 'block'; })
        .finally(function () { btn.disabled = false; btn.textContent = 'Dat lai'; });
}

// ══════════════════════════════════════════════════════════
// MODAL: Sua thong tin bac si
// ══════════════════════════════════════════════════════════
function openEditDoctor(id, fullName, specialty, degree, description) {
    document.getElementById('edDoctorId').value    = id;
    document.getElementById('edFullName').value    = fullName    || '';
    document.getElementById('edSpecialty').value   = specialty   || '';
    document.getElementById('edDegree').value      = degree      || '';
    document.getElementById('edDescription').value = description || '';
    ['edErr', 'edOk'].forEach(function (x) { document.getElementById(x).style.display = 'none'; });
    document.getElementById('editDoctorModal').classList.add('open');
}
function closeEditDoctor() { document.getElementById('editDoctorModal').classList.remove('open'); }
function saveEditDoctor() {
    var id  = document.getElementById('edDoctorId').value;
    var btn = document.getElementById('edSaveBtn');
    var errEl = document.getElementById('edErr'); var okEl = document.getElementById('edOk');
    errEl.style.display = 'none'; okEl.style.display = 'none'; btn.disabled = true; btn.textContent = 'Dang luu...';
    fetch('/Admin/UpdateDoctor?id=' + id, {
        method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrf },
        body: JSON.stringify({ fullName: document.getElementById('edFullName').value.trim(), specialty: document.getElementById('edSpecialty').value.trim(), degree: document.getElementById('edDegree').value.trim(), description: document.getElementById('edDescription').value.trim() })
    })
    .then(function (r) { return r.json(); })
    .then(function (d) {
        if (d.success || d.Success) { okEl.textContent = 'Da cap nhat thanh cong.'; okEl.style.display = 'block'; showToast('Cap nhat bac si thanh cong!', true); tabLoaded['doctors'] = false; setTimeout(function () { closeEditDoctor(); loadDoctors(); }, 1200); }
        else { errEl.textContent = d.message || d.Message || 'Lỗi.'; errEl.style.display = 'block'; }
    })
    .catch(function () { errEl.textContent = 'Loi ket noi.'; errEl.style.display = 'block'; })
    .finally(function () { btn.disabled = false; btn.textContent = 'Luu thay doi'; });
}

// ══════════════════════════════════════════════════════════
// TAB: TIN NHẮN (Admin Inbox)
// ══════════════════════════════════════════════════════════
var admInboxHubConn = null;
var admInboxConvId  = null;
var admInboxMyId    = 0;
var _admSeenIds     = new Set();
var admInboxInited  = false;

function initAdminInbox() {
    if (!admInboxInited) {
        admInboxInited = true;
        admInboxLoadConvs();
        admInboxConnectHub();
    }
}

function admInboxLoadConvs() {
    fetch('/api/msgs/conversations')
    .then(function(r){ return r.json(); })
    .then(function(convs) {
        var list = document.getElementById('admInboxConvList');
        if (!convs || !convs.length) {
            list.innerHTML = '<div style="padding:14px;text-align:center;font-size:12px;color:#94a3b8;">Chưa có tin nhắn.</div>';
            return;
        }
        list.innerHTML = '';
        convs.forEach(function(c) {
            var item = document.createElement('div');
            item.className = 'adm-inbox-conv-item' + (c.id === admInboxConvId ? ' active' : '');
            item.onclick = function() { admInboxSelectConv(c); };
            var lastMsg = c.lastMessage || c.LastMessage || '';
            var lastTime = '';
            if (c.lastMessageAt || c.LastMessageAt) {
                var d = new Date(c.lastMessageAt || c.LastMessageAt);
                lastTime = d.getHours().toString().padStart(2,'0') + ':' + d.getMinutes().toString().padStart(2,'0');
            }
            var hasUnread = (c.unreadCount || c.UnreadCount || 0) > 0;
            item.innerHTML =
                '<div class="adm-inbox-conv-name">' +
                    '<span>' + (c.title || c.Title || 'Cuộc trò chuyện') + '</span>' +
                    (hasUnread ? '<span class="adm-inbox-unread-dot"></span>' : '') +
                '</div>' +
                '<div style="display:flex;justify-content:space-between;align-items:center;">' +
                    '<div class="adm-inbox-conv-preview">' + (lastMsg || 'Chưa có tin nhắn') + '</div>' +
                    '<div class="adm-inbox-conv-time">' + lastTime + '</div>' +
                '</div>';
            list.appendChild(item);
        });
    }).catch(function(){});
}

function admInboxSelectConv(conv) {
    admInboxConvId = conv.id;
    document.querySelectorAll('.adm-inbox-conv-item').forEach(function(el){ el.classList.remove('active'); });
    event.currentTarget.classList.add('active');

    document.getElementById('admInboxEmpty').style.display = 'none';
    var ca = document.getElementById('admInboxChatArea');
    ca.style.display = 'flex';

    var title = conv.title || conv.Title || 'Cuộc trò chuyện';
    document.getElementById('admInboxHdAv').textContent = title[0].toUpperCase();
    document.getElementById('admInboxHdName').textContent = title;

    admInboxLoadMessages(conv.id);
    if (admInboxHubConn && admInboxHubConn.state === 'Connected')
        admInboxHubConn.invoke('JoinConversation', conv.id).catch(function(){});
    fetch('/api/msgs/conversations/' + conv.id + '/read', { method: 'PUT' });
}

function admInboxLoadMessages(convId) {
    fetch('/api/msgs/conversations/' + convId + '/messages')
    .then(function(r){ return r.json(); })
    .then(function(msgs) {
        var box = document.getElementById('admInboxMsgs');
        box.innerHTML = '';
        if (!msgs || !msgs.length) {
            box.innerHTML = '<div style="text-align:center;padding:20px;font-size:12px;color:#94a3b8;">Chưa có tin nhắn.</div>';
            return;
        }
        msgs.forEach(function(m){ admInboxAppendMsg(m, false); });
        box.scrollTop = box.scrollHeight;
    });
}

function admInboxAppendMsg(msg, scroll) {
    var box    = document.getElementById('admInboxMsgs');
    var isMine = (msg.senderId === admInboxMyId || msg.SenderId === admInboxMyId);
    var cls    = isMine ? 'mine' : 'other';
    var wrap   = document.createElement('div');
    wrap.className = 'adm-inbox-msg ' + cls;
    var av = document.createElement('div');
    av.className = 'adm-inbox-msg-av';
    var senderName = msg.senderName || msg.SenderName || '?';
    av.textContent = isMine ? 'T' : senderName[0].toUpperCase();
    var col  = document.createElement('div');
    col.className = 'adm-inbox-msg-body';
    var bub  = document.createElement('div');
    bub.className = 'adm-inbox-bub';
    bub.textContent = msg.content || msg.Content;
    var meta = document.createElement('div');
    meta.className = 'adm-inbox-msg-meta';
    var d = new Date(msg.sentAt || msg.SentAt);
    meta.textContent = (!isMine ? senderName + ' · ' : '') +
        d.getHours().toString().padStart(2,'0') + ':' + d.getMinutes().toString().padStart(2,'0');
    col.appendChild(bub); col.appendChild(meta);
    wrap.appendChild(av); wrap.appendChild(col);
    box.appendChild(wrap);
    if (scroll) box.scrollTop = box.scrollHeight;
}

function admInboxSend() {
    if (!admInboxConvId) return;
    var input = document.getElementById('admInboxInput');
    var msg = input.value.trim();
    if (!msg) return;
    admInboxAppendMsg({ senderId: admInboxMyId, content: msg, sentAt: new Date().toISOString(), conversationId: admInboxConvId }, true);
    input.value = '';
    if (admInboxHubConn && admInboxHubConn.state === 'Connected') {
        admInboxHubConn.invoke('SendMessage', admInboxConvId, msg).catch(function(){ admInboxSendRest(msg); });
    } else { admInboxSendRest(msg); }
}

function admInboxSendRest(content) {
    fetch('/api/msgs/conversations/' + admInboxConvId + '/send', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ content: content })
    }).catch(function(){});
}

function admInboxConnectHub() {
    fetch('/api/msgs/token')
    .then(function(r){ return r.json(); })
    .then(function(data) {
        if (!data.token) return;
        try {
            var parts = data.token.split('.');
            if (parts.length === 3) {
                var payload = JSON.parse(atob(parts[1].replace(/-/g,'+').replace(/_/g,'/')));
                admInboxMyId = parseInt(
                    payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
                    || payload['sub'] || '0');
            }
        } catch(e){}

        admInboxHubConn = new signalR.HubConnectionBuilder()
            .withUrl('https://localhost:7100/hubs/chat?access_token=' + encodeURIComponent(data.token))
            .withAutomaticReconnect()
            .build();

        admInboxHubConn.on('ReceiveMessage', function(msg) {
            var msgId = msg.id || msg.Id;
            if (msgId && _admSeenIds.has(msgId)) return;
            if (msgId) _admSeenIds.add(msgId);
            var senderId = msg.senderId || msg.SenderId;
            if (senderId === admInboxMyId) return;

            var convId = msg.conversationId || msg.ConversationId;
            if (convId === admInboxConvId) {
                admInboxAppendMsg(msg, true);
                fetch('/api/msgs/conversations/' + convId + '/read', { method: 'PUT' });
            } else {
                var b = document.getElementById('admInboxBadge');
                b.textContent = (parseInt(b.textContent || '0') + 1);
                b.style.display = 'inline';
                admInboxLoadConvs();
            }
        });

        admInboxHubConn.onreconnected(function() {
            if (admInboxConvId) admInboxHubConn.invoke('JoinConversation', admInboxConvId).catch(function(){});
        });

        admInboxHubConn.start()
        .then(function() {
            if (admInboxConvId) admInboxHubConn.invoke('JoinConversation', admInboxConvId).catch(function(){});
        })
        .catch(function(err){ console.warn('Admin inbox SignalR:', err); });
    });
}

function admInboxOpenNewModal() {
    fetch('/api/msgs/internal-users')
    .then(function(r){ return r.json(); })
    .then(function(users) {
        var sel = document.getElementById('admInboxTargetSel');
        sel.innerHTML = '<option value="">-- Chọn người nhận --</option>';
        users.forEach(function(u) {
            var o = document.createElement('option');
            o.value = u.id || u.Id;
            o.textContent = (u.name || u.Name) + ' (' + (u.role || u.Role) + ')';
            sel.appendChild(o);
        });
        document.getElementById('admInboxNewModal').style.display = 'flex';
    });
}

function admInboxCloseNewModal() {
    document.getElementById('admInboxNewModal').style.display = 'none';
}

function admInboxStartNew() {
    var targetId = parseInt(document.getElementById('admInboxTargetSel').value);
    if (!targetId) { showToast('Vui lòng chọn người nhận.', false); return; }
    fetch('/api/msgs/conversations/internal', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ targetUserId: targetId })
    })
    .then(function(r){ return r.json(); })
    .then(function(conv) {
        admInboxCloseNewModal();
        admInboxLoadConvs();
        if (conv && conv.id) admInboxSelectConv(conv);
    }).catch(function(){ showToast('Không thể tạo cuộc trò chuyện.', false); });
}

// ══════════════════════════════════════════════════════════
// ADMIN CONFIRM MODAL (hộp thoại xác nhận dùng chung)
// ══════════════════════════════════════════════════════════
(function () {
    var overlay = document.createElement('div');
    overlay.id = 'adminConfirmOverlay';
    overlay.style.cssText = 'display:none;position:fixed;inset:0;background:rgba(0,0,0,.45);z-index:9999;display:none;align-items:center;justify-content:center;';
    overlay.innerHTML = [
        '<div style="background:#fff;border-radius:16px;padding:28px 32px;width:380px;max-width:94vw;box-shadow:0 20px 60px rgba(0,0,0,.25);text-align:center;">',
        '  <div id="admCfIcon"  style="font-size:40px;margin-bottom:10px;"></div>',
        '  <div id="admCfTitle" style="font-size:17px;font-weight:700;color:#0a4d7c;margin-bottom:8px;"></div>',
        '  <div id="admCfMsg"   style="font-size:14px;color:#4a6c8c;margin-bottom:22px;line-height:1.5;"></div>',
        '  <div style="display:flex;gap:10px;">',
        '    <button id="admCfCancel" style="flex:1;height:40px;background:#f0f0f0;color:#555;border:none;border-radius:10px;font-size:13px;cursor:pointer;font-weight:600;">Huỷ</button>',
        '    <button id="admCfOk"     style="flex:2;height:40px;border:none;border-radius:10px;font-size:13px;cursor:pointer;font-weight:700;color:#fff;">OK</button>',
        '  </div>',
        '</div>'
    ].join('');
    document.body.appendChild(overlay);

    var _onOk = null;
    overlay.querySelector('#admCfCancel').onclick = function () { overlay.style.display = 'none'; };
    overlay.querySelector('#admCfOk').onclick = function () {
        overlay.style.display = 'none';
        if (typeof _onOk === 'function') _onOk();
    };

    window.adminConfirm = function (opts) {
        document.getElementById('admCfIcon').textContent  = opts.icon  || '❓';
        document.getElementById('admCfTitle').textContent = opts.title || 'Xác nhận';
        document.getElementById('admCfMsg').textContent   = opts.msg   || '';
        var okBtn = document.getElementById('admCfOk');
        okBtn.textContent        = opts.okLabel || 'Xác nhận';
        okBtn.style.background   = opts.okColor || '#0a4d7c';
        _onOk = opts.onOk || null;
        overlay.style.display = 'flex';
    };
})();
