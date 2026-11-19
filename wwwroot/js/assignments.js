// Topshiriqlar sahifalari uchun umumiy yordamchi skript:
//   * tez shablonlar (o'qituvchi formasi),
//   * muddatni tez tanlash,
//   * faylni sudrab tashlash zonasi,
//   * "Fayl / Matn" tablari,
//   * matematik belgilar paneli va KaTeX bilan jonli ko'rinish.
(function () {
    'use strict';

    function pick(selector, root) {
        return (root || document).querySelector(selector);
    }

    function pickAll(selector, root) {
        return Array.prototype.slice.call((root || document).querySelectorAll(selector));
    }

    function escapeHtml(value) {
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function setField(id, value) {
        if (value === undefined || value === null) return;
        var el = document.getElementById(id);
        if (!el) return;
        el.value = value;
        el.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // ---------- Tez shablonlar ----------
    if (document.getElementById('Title')) {
        pickAll('[data-tpl-title]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                setField('Title', btn.dataset.tplTitle);
                setField('Description', btn.dataset.tplDesc);
                setField('MaxPoints', btn.dataset.tplPoints);
                setField('SubmissionMode', btn.dataset.tplMode);

                var late = document.getElementById('AllowLate');
                if (late && btn.dataset.tplLate) {
                    late.checked = btn.dataset.tplLate === 'true';
                }

                pickAll('[data-tpl-title]').forEach(function (b) {
                    b.classList.remove('is-active');
                });
                btn.classList.add('is-active');

                var title = document.getElementById('Title');
                if (title) title.focus();
            });
        });
    }

    // ---------- Muddatni tez tanlash (joriy vaqtga tayanadi) ----------
    (function () {
        var due = document.getElementById('DueDate');
        if (!due) return;

        function pad(n) { return String(n).padStart(2, '0'); }
        function toLocal(d) {
            return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate())
                + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes());
        }

        pickAll('[data-due]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var now = new Date();
                var mode = btn.dataset.due;
                var d = new Date(now.getTime());

                if (mode === '+1h') {
                    d.setHours(d.getHours() + 1);
                } else if (mode === 'today-2359') {
                    d.setHours(23, 59, 0, 0);
                } else if (mode === 'tomorrow-2359') {
                    d.setDate(d.getDate() + 1);
                    d.setHours(23, 59, 0, 0);
                } else if (mode === '+1w') {
                    d.setDate(d.getDate() + 7);
                }

                due.value = toLocal(d);
            });
        });
    })();

    // ---------- Faylni sudrab tashlash zonasi ----------
    pickAll('[data-dropzone]').forEach(function (zone) {
        var input = pick('input[type="file"]', zone);
        if (!input) return;

        var picked = pick('[data-file-picked]', zone);

        function showPicked() {
            if (!picked) return;
            var hasFile = input.files && input.files.length > 0;
            if (!hasFile) {
                picked.hidden = true;
                picked.textContent = '';
                return;
            }
            picked.hidden = false;
            picked.innerHTML = '<i class="bi bi-check2-circle"></i> ' + escapeHtml(input.files[0].name);
        }

        input.addEventListener('change', showPicked);
        showPicked();

        ['dragenter', 'dragover'].forEach(function (evt) {
            zone.addEventListener(evt, function (e) {
                e.preventDefault();
                e.stopPropagation();
                zone.classList.add('is-dragover');
            });
        });

        ['dragleave', 'dragend', 'drop'].forEach(function (evt) {
            zone.addEventListener(evt, function (e) {
                e.preventDefault();
                e.stopPropagation();
                zone.classList.remove('is-dragover');
            });
        });

        zone.addEventListener('drop', function (e) {
            if (!e.dataTransfer || !e.dataTransfer.files || !e.dataTransfer.files.length) return;
            try {
                input.files = e.dataTransfer.files;
                showPicked();
            } catch (err) {
                // Ba'zi brauzerlar faylni dastur orqali qo'yishga ruxsat bermaydi —
                // foydalanuvchi faylni bosib tanlashi mumkin.
            }
        });
    });

    // ---------- "Fayl / Matn" tablari ----------
    pickAll('[data-answer]').forEach(function (answer) {
        var tabs = pickAll('[data-answer-tab]', answer);
        if (!tabs.length) return;

        tabs.forEach(function (tab) {
            tab.addEventListener('click', function () {
                tabs.forEach(function (t) {
                    t.classList.toggle('is-active', t === tab);
                });
                pickAll('[data-answer-panel]', answer).forEach(function (panel) {
                    panel.classList.toggle('is-hidden', panel.dataset.answerPanel !== tab.dataset.answerTab);
                });
                var active = pick('[data-answer-panel]:not(.is-hidden) [data-math-textarea]', answer);
                if (active) active.focus();
            });
        });

        // Bitta javob turi bo'lsa (faqat fayl / faqat matn) — tab ko'rsatilmaydi,
        // lekin fayl maydoni bo'lsa fokus uchun hech narsa qilish shart emas.
    });

    // ---------- Matematik belgilar paneli ----------
    function insertAtCursor(textarea, text) {
        var start = textarea.selectionStart != null ? textarea.selectionStart : textarea.value.length;
        var end = textarea.selectionEnd != null ? textarea.selectionEnd : textarea.value.length;
        var value = textarea.value;

        textarea.value = value.slice(0, start) + text + value.slice(end);

        var caret = start + text.length;
        var braces = text.indexOf('{}');
        if (braces !== -1) {
            caret = start + braces + 1;
        } else if (text === '$...$') {
            caret = start + 1;
        }

        textarea.focus();
        if (textarea.setSelectionRange) {
            textarea.setSelectionRange(caret, caret);
        }
        textarea.dispatchEvent(new Event('input', { bubbles: true }));
    }

    pickAll('[data-insert]').forEach(function (btn) {
        btn.addEventListener('click', function (e) {
            e.preventDefault();
            var host = btn.closest('[data-answer]') || document;
            var textarea = pick('[data-math-textarea]', host);
            if (!textarea) return;
            insertAtCursor(textarea, btn.dataset.insert || '');
        });
    });

    // ---------- Matnli javobning jonli ko'rinishi (KaTeX) ----------
    var previewDelimiters = [
        { left: '$$', right: '$$', display: true },
        { left: '$', right: '$', display: false },
        { left: '\\[', right: '\\]', display: true },
        { left: '\\(', right: '\\)', display: false }
    ];

    function renderPreview(textarea) {
        var host = textarea.closest('[data-answer]');
        var preview = host ? pick('[data-math-preview]', host) : null;
        if (!preview) return;

        var value = textarea.value.trim();
        if (!value) {
            preview.hidden = true;
            preview.textContent = '';
            return;
        }

        preview.hidden = false;
        // textContent — kiritilgan HTML hech qachon bajarilmaydi (XSS himoyasi).
        preview.textContent = value;

        if (window.renderMathInElement) {
            try {
                window.renderMathInElement(preview, {
                    delimiters: previewDelimiters,
                    throwOnError: false
                });
            } catch (err) {
                // Formula xato bo'lsa ham matn o'z holida qoladi.
            }
        }
    }

    pickAll('[data-math-textarea]').forEach(function (textarea) {
        textarea.addEventListener('input', function () { renderPreview(textarea); });
        if (textarea.value.trim()) renderPreview(textarea);
    });

    // ---------- Bo'sh formani yuborishga yo'l qo'ymaslik ----------
    pickAll('.iz-answer-form').forEach(function (form) {
        form.addEventListener('submit', function (e) {
            var fileInput = pick('input[type="file"]', form);
            var textarea = pick('[data-math-textarea]', form);
            var error = pick('[data-answer-error]', form);

            var hasFile = fileInput && fileInput.files && fileInput.files.length > 0;
            var hasText = textarea && textarea.value.trim().length > 0;

            if (!hasFile && !hasText) {
                e.preventDefault();
                if (error) {
                    error.textContent = textarea
                        ? 'Fayl yuklang yoki javobni matn bilan yozing.'
                        : 'Faylni tanlang.';
                    error.hidden = false;
                }
                if (textarea && pick('[data-answer-panel="text"]', form) && !pick('[data-answer-panel="text"]', form).classList.contains('is-hidden')) {
                    textarea.focus();
                } else if (fileInput) {
                    fileInput.focus();
                }
                return;
            }

            if (error) error.hidden = true;
        });
    });

    // ---------- Fayl hajmi va "yuklanmoqda" holati ----------
    // Katta fayl sekin internetda uzoq yuklanadi. Xatoni yuborishdan oldin
    // ko'rsatamiz va yuborilayotganda tugmani bloklab, foydalanuvchi sahifani
    // qayta yubormasligi/yopmasligi uchun aniq holat ko'rsatamiz.
    function megabytes(bytes) {
        return Math.round((bytes / (1024 * 1024)) * 10) / 10;
    }

    pickAll('[data-upload-form], .iz-answer-form').forEach(function (form) {
        var button = pick('button[type="submit"]', form);
        var status = pick('[data-upload-status]', form);
        var error = pick('[data-answer-error]', form);

        function showStatus(text, isError) {
            var host = status || error;
            if (!host) return;
            host.hidden = false;
            host.textContent = text;
            host.classList.toggle('is-error', !!isError);
        }

        form.addEventListener('submit', function (e) {
            // Boshqa tekshiruv allaqachon to'xtatgan bo'lsa — tegmaymiz.
            if (e.defaultPrevented || form.dataset.submitting === '1') return;

            var input = pick('input[type="file"]', form);
            if (!input || !input.files || !input.files.length) return;

            var size = input.files[0].size;
            var maxMb = parseFloat(input.dataset.maxMb || form.dataset.maxMb || '0');

            if (maxMb > 0 && size > maxMb * 1024 * 1024) {
                e.preventDefault();
                showStatus('Fayl hajmi ' + maxMb + ' MB dan oshmasligi kerak. ' +
                    'Tanlangan fayl: ' + megabytes(size) + ' MB.', true);
                input.focus();
                return;
            }

            if (size > 3 * 1024 * 1024) {
                showStatus('Fayl yuklanmoqda (' + megabytes(size) + ' MB). Sahifani yopmang — ' +
                    'sekin internetda bu 1-2 daqiqa davom etishi mumkin.');
            } else {
                showStatus('Yuborilmoqda…');
            }

            if (button) {
                button.dataset.originalHtml = button.innerHTML;
                button.disabled = true;
                button.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Yuklanmoqda…';
            }
            form.dataset.submitting = '1';
        });
    });

    // Orqaga qaytganda (bfcache) tugma bloklangan holda qolmasin.
    window.addEventListener('pageshow', function () {
        pickAll('form[data-submitting="1"]').forEach(function (form) {
            delete form.dataset.submitting;
            var button = pick('button[type="submit"]', form);
            if (button) {
                button.disabled = false;
                if (button.dataset.originalHtml) {
                    button.innerHTML = button.dataset.originalHtml;
                }
            }
        });
    });
})();
