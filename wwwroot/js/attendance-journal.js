(function () {
    'use strict';

    const root = document.getElementById('attendanceJournal');
    if (!root) return;

    const saveUrl = root.dataset.saveUrl;
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenInput ? tokenInput.value : '';
    const popover = document.getElementById('cellPopover');
    const toast = document.getElementById('journalToast');

    const STATUS_CLASSES = ['is-keldi', 'is-kelmadi', 'is-kech', 'is-sababli', 'is-empty'];
    const STATUS_CLASS = {
        Keldi: 'is-keldi',
        Kelmadi: 'is-kelmadi',
        KechQoldi: 'is-kech',
        Sababli: 'is-sababli'
    };
    const STATUS_SYMBOL = { Keldi: '+', Kelmadi: '−', KechQoldi: 'K', Sababli: 'S' };
    const STATUS_LABEL = { Keldi: 'Keldi', Kelmadi: 'Kelmadi', KechQoldi: 'Kech qoldi', Sababli: 'Sababli' };
    const KEY_STATUS = { '1': 'Keldi', '2': 'Kelmadi', '3': 'KechQoldi', '4': 'Sababli' };

    let activeCell = null;
    let toastTimer = null;

    function cellsInRow(row) {
        return Array.from(row.querySelectorAll('.iz-att-cell[data-student-id]'));
    }

    function applyStatus(cell, status) {
        cell.dataset.status = status || '';
        cell.textContent = STATUS_SYMBOL[status] || '';
        cell.classList.remove(...STATUS_CLASSES);
        cell.classList.add(STATUS_CLASS[status] || 'is-empty');

        const row = cell.closest('tr');
        const nameEl = row ? row.querySelector('.iz-journal-fullname') : null;
        const student = nameEl ? nameEl.textContent.trim() : '';
        const label = STATUS_LABEL[status] || 'Belgilanmagan';
        cell.setAttribute('aria-label', `${student}, ${cell.dataset.date}: ${label}`);
    }

    function recalcRow(row) {
        if (!row) return;

        const counts = { Keldi: 0, Kelmadi: 0, KechQoldi: 0, Sababli: 0 };
        cellsInRow(row).forEach(function (cell) {
            const status = cell.dataset.status;
            if (status && counts[status] !== undefined) counts[status]++;
        });

        Object.keys(counts).forEach(function (key) {
            const target = row.querySelector('[data-total="' + key + '"]');
            if (target) target.textContent = counts[key];
        });

        const total = counts.Keldi + counts.Kelmadi + counts.KechQoldi + counts.Sababli;
        const percent = total ? Math.round((counts.Keldi / total) * 100) : 0;
        const percentTarget = row.querySelector('[data-total-percent]');
        if (percentTarget) percentTarget.textContent = percent + '%';
    }

    function showToast(message, isError) {
        if (!toast) return;

        toast.textContent = message;
        toast.classList.toggle('is-error', !!isError);
        toast.hidden = false;
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { toast.hidden = true; }, 3200);
    }

    async function saveCell(cell, status) {
        const previous = cell.dataset.status || '';
        if (previous === status) return;

        applyStatus(cell, status);
        recalcRow(cell.closest('tr'));
        cell.classList.add('is-saving');

        try {
            const body = new URLSearchParams();
            body.set('subjectId', root.dataset.subjectId || '');
            body.set('groupId', root.dataset.groupId || '');
            body.set('studentId', cell.dataset.studentId || '');
            body.set('date', cell.dataset.date || '');
            body.set('status', status);
            body.set('__RequestVerificationToken', token);

            const response = await fetch(saveUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
                body: body.toString(),
                credentials: 'same-origin'
            });

            if (!response.ok) {
                let message = 'Saqlab bo‘lmadi. Qayta urinib ko‘ring.';
                try {
                    const data = await response.json();
                    if (data && data.message) message = data.message;
                } catch (e) {
                    // Server JSON qaytarmagan bo'lsa, umumiy xabar qoladi.
                }
                throw new Error(message);
            }

            cell.classList.add('is-saved');
            setTimeout(function () { cell.classList.remove('is-saved'); }, 800);
        } catch (error) {
            applyStatus(cell, previous);
            recalcRow(cell.closest('tr'));
            showToast(error.message || 'Saqlab bo‘lmadi.', true);
        } finally {
            cell.classList.remove('is-saving');
        }
    }

    function openPopover(cell) {
        if (!popover) return;

        activeCell = cell;
        popover.hidden = false;
        popover.querySelectorAll('[data-set-status]').forEach(function (button) {
            button.classList.toggle('is-active', button.dataset.setStatus === cell.dataset.status);
        });

        const isMobile = window.matchMedia('(max-width: 767.98px)').matches;
        popover.classList.toggle('is-sheet', isMobile);

        if (isMobile) {
            popover.style.left = '';
            popover.style.top = '';
            return;
        }

        const rect = cell.getBoundingClientRect();
        const width = popover.offsetWidth || 230;
        const height = popover.offsetHeight || 150;
        const margin = 10;

        let left = rect.left + rect.width / 2 - width / 2;
        let top = rect.bottom + margin;

        if (top + height > window.innerHeight - margin) {
            top = rect.top - height - margin;
        }

        left = Math.max(margin, Math.min(left, window.innerWidth - width - margin));
        top = Math.max(margin, top);

        popover.style.left = left + 'px';
        popover.style.top = top + 'px';
    }

    function closePopover() {
        if (!popover) return;

        popover.hidden = true;
        popover.classList.remove('is-sheet');
        activeCell = null;
    }

    root.addEventListener('click', function (event) {
        const cell = event.target.closest('.iz-att-cell[data-student-id]');
        if (!cell) return;
        event.preventDefault();
        openPopover(cell);
    });

    if (popover) {
        popover.addEventListener('click', function (event) {
            const button = event.target.closest('[data-set-status]');
            if (!button || !activeCell) return;

            const cell = activeCell;
            closePopover();
            saveCell(cell, button.dataset.setStatus);
            cell.focus({ preventScroll: true });
        });
    }

    document.addEventListener('click', function (event) {
        if (popover && !popover.hidden
            && !event.target.closest('#cellPopover')
            && !event.target.closest('.iz-att-cell[data-student-id]')) {
            closePopover();
        }
    });

    root.addEventListener('keydown', function (event) {
        const cell = event.target.closest('.iz-att-cell[data-student-id]');
        if (!cell) return;

        if (KEY_STATUS[event.key]) {
            event.preventDefault();
            saveCell(cell, KEY_STATUS[event.key]);
            return;
        }

        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            openPopover(cell);
            return;
        }

        if (event.key === 'Escape') {
            closePopover();
            return;
        }

        const row = cell.closest('tr');
        const rows = Array.from(root.querySelectorAll('tbody tr[data-student-id]'));
        let target = null;

        if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
            const rowCells = cellsInRow(row);
            const index = rowCells.indexOf(cell);
            target = rowCells[index + (event.key === 'ArrowRight' ? 1 : -1)];
        } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            const rowIndex = rows.indexOf(row);
            const nextRow = rows[rowIndex + (event.key === 'ArrowDown' ? 1 : -1)];
            if (nextRow) {
                target = nextRow.querySelector('.iz-att-cell[data-date="' + cell.dataset.date + '"]');
            }
        }

        if (target) {
            event.preventDefault();
            target.focus();
        }
    });

    window.addEventListener('scroll', closePopover, true);
    window.addEventListener('resize', closePopover);

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') closePopover();
    });
})();
