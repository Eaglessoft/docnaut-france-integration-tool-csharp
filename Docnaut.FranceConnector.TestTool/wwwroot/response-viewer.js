(() => {
    'use strict';

    const element = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    };
    const isRecord = value => value !== null && typeof value === 'object' && !Array.isArray(value);
    const scalar = value => value === null ? 'null' : value === '' ? 'Empty string' : String(value);

    function render(value, depth = 0, budget = { remaining: 1500 }) {
        if (--budget.remaining < 0) return element('span', 'muted', 'Preview limit reached. See Pretty JSON for the complete response.');
        if (value === null || typeof value !== 'object') {
            return element('span', `result-value result-${value === null ? 'null' : typeof value}`, scalar(value));
        }
        // Keep deeply nested payloads available without exhausting the browser's call stack.
        if (depth >= 12) return element('span', 'muted', 'Nested value. See Pretty JSON for the complete response.');
        if (Array.isArray(value)) {
            if (!value.length) return element('p', 'response-empty', 'No items returned.');
            const container = element('div', 'result-collection');
            const preview = value.slice(0, 100);
            container.append(element('p', 'result-count', `${value.length} ${value.length === 1 ? 'item' : 'items'}${value.length > preview.length ? ' · Showing the first 100; see Pretty JSON for all items.' : ''}`));
            if (preview.every(isRecord)) {
                const allColumns = [...new Set(preview.flatMap(item => Object.keys(item)))];
                const columns = allColumns.slice(0, 20);
                if (allColumns.length > columns.length) container.append(element('p', 'response-empty', 'Showing the first 20 fields. See Pretty JSON for all fields.'));
                if (columns.length) {
                    const scroll = element('div', 'result-table-scroll');
                    scroll.tabIndex = 0;
                    scroll.setAttribute('role', 'region');
                    scroll.setAttribute('aria-label', 'Response items');
                    const table = element('table', 'result-table');
                    const head = element('thead');
                    const heading = element('tr');
                    columns.forEach(key => {
                        const cell = element('th', '', key);
                        cell.scope = 'col';
                        heading.append(cell);
                    });
                    head.append(heading);
                    const body = element('tbody');
                    preview.forEach(item => {
                        const row = element('tr');
                        columns.forEach(key => {
                            const cell = element('td');
                            cell.append(Object.hasOwn(item, key) ? render(item[key], depth + 1, budget) : element('span', 'muted', '—'));
                            row.append(cell);
                        });
                        body.append(row);
                    });
                    table.append(head, body);
                    scroll.append(table);
                    container.append(scroll);
                    return container;
                }
            }
            const list = element('ol', 'result-list');
            preview.forEach(item => {
                const row = element('li');
                row.append(render(item, depth + 1, budget));
                list.append(row);
            });
            container.append(list);
            return container;
        }
        const entries = Object.entries(value);
        if (!entries.length) return element('p', 'response-empty', 'Empty object.');
        const details = element('dl', 'result-details');
        entries.slice(0, 100).forEach(([key, item]) => {
            const row = element('div', 'result-detail-row');
            row.append(element('dt', '', key));
            const description = element('dd');
            description.append(render(item, depth + 1, budget));
            row.append(description);
            details.append(row);
        });
        if (entries.length > 100) details.append(element('p', 'response-empty', 'Showing the first 100 fields. See Pretty JSON for all fields.'));
        return details;
    }

    // Format lexical tokens so long integers, decimal precision and exponent notation survive copying.
    function prettyJson(raw) {
        const tokens = raw.match(/"(?:\\.|[^"\\])*"|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?|true|false|null|[{}\[\],:]/g) || [];
        let nesting = 0;
        for (const token of tokens) {
            if (token === '{' || token === '[') nesting++;
            else if (token === '}' || token === ']') nesting--;
            if (nesting > 64) return { content: raw, deepResponse: true };
        }
        let output = '';
        let indent = 0;
        let unsafeNumbers = false;
        tokens.forEach((token, index) => {
            if (/^-?\d/.test(token)) {
                const digits = token.split(/[eE]/)[0].replace(/[^0-9]/g, '').replace(/^0+/, '');
                unsafeNumbers ||= digits.length > 15 || !Number.isFinite(Number(token));
            }
            const newline = () => { output += '\n' + '  '.repeat(indent); };
            if (token === '{' || token === '[') {
                output += token;
                if (tokens[index + 1] !== (token === '{' ? '}' : ']')) { indent++; newline(); }
            } else if (token === '}' || token === ']') {
                if (tokens[index - 1] !== (token === '}' ? '{' : '[')) { indent--; newline(); }
                output += token;
            } else if (token === ',') { output += token; newline(); }
            else if (token === ':') output += ': ';
            else output += token;
        });
        return { content: output, unsafeNumbers };
    }

    async function copyText(text) {
        if (navigator.clipboard && window.isSecureContext) {
            try {
                await navigator.clipboard.writeText(text);
                return;
            } catch { /* Clipboard permissions can be denied; try the local fallback. */ }
        }
        const previous = document.activeElement;
        const input = element('textarea', 'clipboard-buffer');
        input.value = text;
        input.setAttribute('aria-label', 'Response to copy');
        document.body.append(input);
        input.select();
        try {
            if (!document.execCommand('copy')) throw new Error('Copy unavailable');
        } finally {
            input.remove();
            previous?.focus();
        }
    }

    document.querySelectorAll('[data-response-viewer]').forEach((viewer, index) => {
        const source = viewer.querySelector('.response-source');
        if (!source) return;
        const raw = source.textContent;
        const largeResponse = raw.length > 1000000;
        let value;
        let validJson = false;
        try { if (!largeResponse) { value = JSON.parse(raw); validJson = true; } } catch { /* Retain non-JSON bodies as encoded text. */ }
        const formatted = validJson ? prettyJson(raw) : { content: raw, unsafeNumbers: false };
        const content = formatted.content;
        source.textContent = content || 'No response body.';
        const toolbar = element('div', 'response-toolbar');
        const tablist = element('div', 'response-tabs');
        tablist.setAttribute('role', 'tablist');
        tablist.setAttribute('aria-label', 'Response view');
        const panels = [element('div', 'response-panel'), element('div', 'response-panel result-panel')];
        const tabs = ['Pretty JSON', 'Results'].map((label, tabIndex) => {
            const tab = element('button', 'response-tab', label);
            tab.type = 'button';
            tab.id = `response-${index}-tab-${tabIndex}`;
            tab.setAttribute('role', 'tab');
            tab.setAttribute('aria-controls', `response-${index}-panel-${tabIndex}`);
            const panel = panels[tabIndex];
            panel.id = `response-${index}-panel-${tabIndex}`;
            panel.setAttribute('role', 'tabpanel');
            panel.setAttribute('aria-labelledby', tab.id);
            panel.tabIndex = 0;
            tablist.append(tab);
            return tab;
        });
        const activate = active => {
            tabs.forEach((tab, tabIndex) => {
                tab.setAttribute('aria-selected', String(tabIndex === active));
                tab.tabIndex = tabIndex === active ? 0 : -1;
                panels[tabIndex].hidden = tabIndex !== active;
            });
        };
        tabs.forEach((tab, tabIndex) => {
            tab.addEventListener('click', () => activate(tabIndex));
            tab.addEventListener('keydown', event => {
                let next;
                if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') next = 1 - tabIndex;
                else if (event.key === 'Home') next = 0;
                else if (event.key === 'End') next = 1;
                else return;
                event.preventDefault();
                activate(next);
                tabs[next].focus();
            });
        });
        const copy = element('button', 'secondary response-copy', validJson ? 'Copy JSON' : 'Copy response');
        copy.type = 'button';
        copy.disabled = !raw;
        const feedback = element('span', 'copy-feedback');
        feedback.setAttribute('role', 'status');
        let feedbackTimer;
        copy.addEventListener('click', async () => {
            clearTimeout(feedbackTimer);
            try {
                await copyText(content);
                feedback.textContent = 'Copied.';
            } catch {
                feedback.textContent = 'Copy unavailable. Select the response text to copy.';
            }
            feedbackTimer = setTimeout(() => { feedback.textContent = ''; }, 5000);
        });
        toolbar.append(tablist, copy, feedback);
        if (largeResponse) panels[0].append(element('p', 'response-empty', 'This response exceeds the preview limit. The complete original text is available below and can be copied.'));
        else if (formatted.deepResponse) panels[0].append(element('p', 'response-empty', 'This response exceeds the nesting limit. The complete original text is available below and can be copied.'));
        else if (!validJson && raw) panels[0].append(element('p', 'response-empty', 'This response is not JSON. Its original text is shown below.'));
        panels[0].append(source);
        panels[1].append(largeResponse
            ? element('p', 'response-empty', 'This response exceeds the preview limit. Open Pretty JSON to inspect and copy the complete original response.')
            : formatted.deepResponse ? element('p', 'response-empty', 'This response exceeds the nesting limit. Open Pretty JSON to inspect and copy the complete original response.')
            : formatted.unsafeNumbers
            ? element('p', 'response-empty', 'This response contains high-precision numbers. Use Pretty JSON to view and copy their exact values.')
            : validJson ? render(value) : element('p', 'response-empty', raw ? 'Results are available for JSON responses. Open Pretty JSON to inspect the original response text.' : 'No response body.'));
        viewer.append(toolbar, ...panels);
        activate(0);
    });
})();
