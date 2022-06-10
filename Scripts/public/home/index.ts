import { comparer } from "../main.js";

const usersTable: HTMLTableElement = (document.getElementById("users") as HTMLTableElement);

const sortingStates: { [key: string]: boolean } = {};

window.onload = () => {
    usersTable.querySelectorAll("th.sortable").forEach(
        th => th.addEventListener(
            "click",
            (
                () => {
                    const sortingKey: string = th.getAttribute("data-sorting-key")!;
                    const table = th.closest("table") as HTMLTableElement;
                    Array.from(table.querySelectorAll("tr:nth-child(n+2)"))
                        .sort(
                            comparer(
                                Array.from(th.parentNode!.children).indexOf(th),
                                sortingStates[sortingKey] = !sortingStates[sortingKey]
                            )
                        )
                        .forEach(tr => table.appendChild(tr));
                    //console.debug(sortingStates[sortingKey]);
                }
            )
        )
    );
}
