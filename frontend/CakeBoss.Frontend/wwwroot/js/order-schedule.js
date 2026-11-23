// Calendário do formulário de pedido: mostra só os dias e horários livres para o tamanho do bolo.
// Quem decide o que está livre é o servidor (GET /pedido?handler=Slots&kg=...); aqui só desenhamos a resposta
// e preenchemos os campos de data e horário, que continuam sendo o que o formulário envia.
// Sem JavaScript, ou se a consulta falhar, os campos comuns de data e horário ficam visíveis.
(() => {
    'use strict';

    const root = document.querySelector('[data-schedule]');
    if (!root) return;

    const form = root.closest('form');
    const picker = root.querySelector('[data-schedule-picker]');
    const fallback = root.querySelector('[data-schedule-fallback]');
    const status = root.querySelector('[data-schedule-status]');
    const calendar = root.querySelector('[data-calendar]');
    const title = root.querySelector('[data-month-title]');
    const grid = root.querySelector('[data-calendar-grid]');
    const timeList = root.querySelector('[data-times]');
    const previous = root.querySelector('[data-month="-1"]');
    const next = root.querySelector('[data-month="1"]');
    const dateInput = document.getElementById('Form_Date');
    const timeInput = document.getElementById('Form_Time');
    const weightInput = document.getElementById('Form_WeightKg');

    const MONTHS = ['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
        'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro'];
    const WEEKDAYS = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S'];

    let byDate = new Map();       // "2026-11-27" -> { date, label, times }
    let firstMonth = null;        // primeiro e último mês com dia livre (índice ano * 12 + mês)
    let lastMonth = null;
    let shownMonth = null;
    let selectedDate = dateInput.value || null;
    let selectedTime = timeInput.value ? timeInput.value.slice(0, 5) : null;
    let lastRequest = 0;
    let debounce = null;

    const pad = (n) => String(n).padStart(2, '0');
    const monthIndex = (iso) => Number(iso.slice(0, 4)) * 12 + Number(iso.slice(5, 7)) - 1;

    function load() {
        const request = ++lastRequest;
        const kg = weightInput ? weightInput.value.trim() : '';
        const url = `${root.dataset.slotsUrl}&kg=${encodeURIComponent(kg)}`;

        fetch(url, { headers: { Accept: 'application/json' } })
            .then((response) => {
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                return response.json();
            })
            .then((data) => {
                if (request === lastRequest) apply(data.days, kg);
            })
            .catch(() => {
                if (request === lastRequest) showFallback();
            });
    }

    function apply(days, kg) {
        byDate = new Map(days.map((day) => [day.date, day]));
        picker.hidden = false;
        fallback.hidden = true;

        let message = kg
            ? 'Mostrando só os dias que comportam o tamanho informado.'
            : 'Informe o tamanho do bolo para ver só os dias que comportam o pedido.';

        // O que estava escolhido pode ter deixado de caber (o cliente aumentou o peso, por exemplo).
        if (selectedDate && !byDate.has(selectedDate)) {
            message = 'O dia escolhido não comporta esse tamanho. Escolha outro dia.';
            select(null, null);
        } else if (selectedDate && selectedTime && !byDate.get(selectedDate).times.includes(selectedTime)) {
            select(selectedDate, null);
        }

        if (days.length === 0) {
            calendar.hidden = true;
            timeList.replaceChildren();
            status.textContent = 'Não há dias livres para esse tamanho nos próximos dias. Fale com a gente para combinar.';
            return;
        }

        calendar.hidden = false;
        firstMonth = monthIndex(days[0].date);
        lastMonth = monthIndex(days[days.length - 1].date);
        if (selectedDate) shownMonth = monthIndex(selectedDate);
        if (shownMonth === null || shownMonth < firstMonth || shownMonth > lastMonth) shownMonth = firstMonth;

        status.textContent = message;
        render();
    }

    function showFallback() {
        picker.hidden = true;
        fallback.hidden = false;
    }

    function select(date, time) {
        selectedDate = date;
        selectedTime = time;
        dateInput.value = date || '';
        timeInput.value = time || '';
    }

    function render() {
        renderCalendar();
        renderTimes();
    }

    function renderCalendar() {
        const year = Math.floor(shownMonth / 12);
        const month = shownMonth % 12;
        const startsOn = new Date(year, month, 1).getDay();
        const daysInMonth = new Date(year, month + 1, 0).getDate();

        title.textContent = `${MONTHS[month]} de ${year}`;
        previous.disabled = shownMonth <= firstMonth;
        next.disabled = shownMonth >= lastMonth;

        const cells = WEEKDAYS.map((letter) => element('span', 'calendar-weekday', letter, { 'aria-hidden': 'true' }));
        for (let i = 0; i < startsOn; i++) cells.push(element('span', 'calendar-day is-empty', ''));

        for (let day = 1; day <= daysInMonth; day++) {
            const iso = `${year}-${pad(month + 1)}-${pad(day)}`;
            const free = byDate.get(iso);

            if (!free) {
                cells.push(element('span', 'calendar-day is-off', String(day), { 'aria-hidden': 'true' }));
                continue;
            }

            cells.push(element('button', 'calendar-day', String(day), {
                type: 'button',
                'data-date': iso,
                'aria-label': free.label,
                'aria-pressed': String(iso === selectedDate)
            }));
        }

        grid.replaceChildren(...cells);
    }

    function renderTimes() {
        const day = selectedDate ? byDate.get(selectedDate) : null;
        if (!day) {
            timeList.replaceChildren(element('p', 'form-hint mb-0', 'Escolha um dia para ver os horários livres.'));
            return;
        }

        const buttons = day.times.map((time) => element('button', 'time-option', time, {
            type: 'button',
            'data-time': time,
            'aria-pressed': String(time === selectedTime)
        }));

        timeList.replaceChildren(
            element('p', 'time-title', `Horários livres em ${day.label}`),
            element('div', 'time-options', null, { role: 'group', 'aria-label': 'Horários livres' }, buttons));
    }

    function element(tag, className, text, attributes = {}, children = []) {
        const node = document.createElement(tag);
        node.className = className;
        if (text !== null) node.textContent = text;
        Object.entries(attributes).forEach(([name, value]) => node.setAttribute(name, value));
        node.append(...children);
        return node;
    }

    grid.addEventListener('click', (event) => {
        const button = event.target.closest('[data-date]');
        if (!button) return;

        const date = button.dataset.date;
        const keepTime = date === selectedDate ? selectedTime : null;
        select(date, keepTime);
        render();
        timeList.querySelector('.time-option')?.focus();
    });

    timeList.addEventListener('click', (event) => {
        const button = event.target.closest('[data-time]');
        if (!button) return;

        select(selectedDate, button.dataset.time);
        renderTimes();
        timeList.querySelector(`[data-time="${selectedTime}"]`)?.focus();
    });

    previous.addEventListener('click', () => {
        shownMonth -= 1;
        renderCalendar();
    });

    next.addEventListener('click', () => {
        shownMonth += 1;
        renderCalendar();
    });

    // O peso muda os dias livres: consulta de novo quando o cliente para de digitar.
    weightInput?.addEventListener('input', () => {
        clearTimeout(debounce);
        debounce = setTimeout(load, 500);
    });

    // Avisa antes de enviar, em vez de o cliente descobrir só na volta do servidor.
    form?.addEventListener('submit', (event) => {
        if (picker.hidden || (dateInput.value && timeInput.value)) return;

        event.preventDefault();
        status.textContent = selectedDate ? 'Escolha o horário do pedido.' : 'Escolha o dia e o horário do pedido.';
        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        root.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });
    });

    load();
})();
