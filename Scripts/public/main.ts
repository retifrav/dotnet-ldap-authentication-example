export const debounce = <F extends (...args: any[]) => any>
    (func: F, waitFor: number) => {
    let timeout = 0;

    return (...args: Parameters<F>): Promise<ReturnType<F>> =>
        new Promise(resolve => {
            if (timeout) { clearTimeout(timeout); }

            timeout = setTimeout(() => resolve(func(...args)), waitFor);
        });
}

export function htmlToElement(html: string): HTMLElement {
    let template = document.createElement("template");
    html = html.trim();
    template.innerHTML = html;
    return template.content.firstChild as HTMLElement;
}

const getCellValue = (tr: HTMLTableRowElement, idx: number) => (
    tr.children[idx] as HTMLTableCellElement
).innerText || tr.children[idx].textContent;

export const comparer = (idx: number, asc: boolean) => (a: HTMLTableRowElement, b: HTMLTableRowElement) => (
    (v1, v2) =>
        v1 !== '' && v2 !== '' && !isNaN(Number(v1)) && !isNaN(Number(v2))
            ? Number(v1) - Number(v2)
            : v1!.toString().localeCompare(v2!)
) (getCellValue(asc ? a : b, idx), getCellValue(asc ? b : a, idx));
