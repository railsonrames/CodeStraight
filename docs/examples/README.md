# Ejemplos / Examples

> 🇪🇸 [Español](#español) · 🇬🇧 [English](#english)

---

# Español

| Archivo | Para qué sirve |
|---|---|
| `.editorconfig` y `.codestraight` | Configuración de equipo completa, con cada clave explicada. Es el ejemplo de la sección 6 del [manual](../MANUAL.md). |
| `demo/DemoCodeStraight.antes.cs` | Archivo con todos los problemas juntos, para probar la extensión o enseñarla. |
| `demo/DemoCodeStraight.despues.cs` | Resultado de referencia, generado con la versión 0.8.2. Con otra versión o configuración puede variar. |

## Probar la demo

1. Copia `.editorconfig` y `.codestraight` a la raíz de un repositorio con un proyecto que tenga `ImplicitUsings`
   activado.
2. Copia `demo/DemoCodeStraight.antes.cs` al proyecto, ábrelo en Visual Studio y abre **Ver → Salida → CodeStraight**.
3. Pulsa **Ctrl+S**: cada acción aparece en la ventana *Salida* con `changed` o `no changes`.
4. Vuelve a guardar: no cambia nada.

El archivo de partida está en Windows-1252 con saltos LF a propósito, para que se vea también la conversión a UTF-8 y
CRLF. `.gitattributes` evita que Git los normalice.

---

# English

| File | What it is for |
|---|---|
| `.editorconfig` and `.codestraight` | A complete team configuration, with every key explained. It is the example in section 6 of the [manual](../MANUAL.md). |
| `demo/DemoCodeStraight.antes.cs` | A file with every problem at once, to try or show the extension. |
| `demo/DemoCodeStraight.despues.cs` | Reference result, generated with version 0.8.2. It may differ with another version or configuration. |

## Trying the demo

1. Copy `.editorconfig` and `.codestraight` to the root of a repository with a project that has `ImplicitUsings`
   enabled.
2. Copy `demo/DemoCodeStraight.antes.cs` into the project, open it in Visual Studio and open
   **View → Output → CodeStraight**.
3. Press **Ctrl+S**: each action shows up in the *Output* window with `changed` or `no changes`.
4. Save again: nothing changes.

The starting file is in Windows-1252 with LF line endings on purpose, so the conversion to UTF-8 and CRLF shows too.
`.gitattributes` keeps Git from normalizing them.
