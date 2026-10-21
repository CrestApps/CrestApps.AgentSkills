# playwright-cli Notes for Orchard Core

Tool-specific behavior that matters when testing an Orchard Core site.

## Install a browser engine

`playwright-cli` needs an installed browser. Without Chrome (for example on macOS) use webkit:

```bash
playwright-cli --browser webkit install
```

Pass `--browser webkit` on the first command that opens a page; the choice sticks for the session.

```bash
playwright-cli --browser webkit open "http://localhost:$PORT/Login"
```

## `open` navigates, `snapshot` re-reads

`playwright-cli open <url>` is a fresh navigation and discards anything typed into a form. Use `snapshot` to re-read the current page and get fresh element refs (for example `e16`). Take a new snapshot after any click that re-renders part of the page.

## `eval` takes a single expression

`playwright-cli eval '<expr>'` runs your text as `() => (<expr>)`.

- Several statements separated by `;`, or `var`/`let` declarations, silently do nothing.
- Wrap multi-statement code in an arrow IIFE, which is itself one expression: `((x)=>{ ...; return ...; })(arg)`.
- When the result is not a function (for example a number), the tool may print a benign `result is not a function` error. Side effects still happen; confirm with a follow-up `eval`.

```bash
playwright-cli eval 'document.getElementById("Password").value'
```

## Password fields need the native setter

The password inputs of the setup wizard (`#Password`, `#PasswordConfirmation`) and the login form (`input[name="LoginForm.Password"]`) sit in a Bootstrap `.input-group` with a show and hide toggle. With `playwright-cli`, `fill`, click then `type`, and a plain `.value =` all leave them empty, while the same commands work on plain inputs. Switching the input to `type="text"` does not help; it is a tool interaction, not an Orchard Core defect.

Set the value through the native `HTMLInputElement` setter and dispatch `input` and `change` so validation and the password strength meter react:

```bash
playwright-cli eval '((p)=>{var s=Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype,"value").set; s.call(p,"Password1!"); p.dispatchEvent(new Event("input",{bubbles:true})); p.dispatchEvent(new Event("change",{bubbles:true})); return p.value.length})(document.querySelector("input[name=\"LoginForm.Password\"]"))'
```

Replace the selector argument for the field you need:

| Field | Selector |
|-------|----------|
| Setup password | `document.getElementById("Password")` |
| Setup confirmation | `document.getElementById("PasswordConfirmation")` |
| Login password | `document.querySelector("input[name=\"LoginForm.Password\"]")` |

Provision with Auto Setup (`autosetup.md`) to avoid the wizard fields; you still need the workaround for the login form.

## Clean up

```bash
playwright-cli session-stop-all
```
