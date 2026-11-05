# Historial de versiones / Release notes

> 🇪🇸 [Español](#español) · 🇬🇧 [English](#english)

---

# Español

## Sin publicar
- `remove_braces`: quitar las llaves de `if`/`else`, `for`, `foreach` y `while` con una sola instrucción simple, según `csharp_prefer_braces` del `.editorconfig` (`false` o `when_multiline`). Una cadena `if`/`else` mantiene el mismo estilo en todas sus ramas y la instrucción conserva su indentación (no mezcla tabuladores y espacios en archivos aún no migrados).

## 0.9.1
- Renombrado: también parámetros, variables locales, funciones locales y genéricos (`applicable_kinds = parameter`, `local`, `local_function`, `type_parameter`), con todas sus referencias.
- Renombrado: un genérico llamado solo `T` cumple la regla del prefijo `T` (antes pasaba a `TT`).
- Renombrado: cuando un renombrado alcanza a otro símbolo del archivo (el método de una interfaz y su implementación `async`), el nombre se recalcula y todo queda resuelto en el mismo guardado (antes hacía falta guardar dos veces para el sufijo `Async`).
- Manual: ejemplo de equipo con todas las reglas de nomenclatura y cada clave del `.codestraight` explicada (sección 6.1 y carpeta `docs/examples`).

## 0.9.0
- Indicador en la barra de estado: verde si CodeStraight está activo para el documento actual, gris si no, naranja si el último guardado tuvo un error. El tooltip muestra el `.codestraight` y las acciones activas; un clic abre *Salida → CodeStraight*.
- `remove_comments`: quitar los comentarios `//` y `/* */`, manteniendo los de documentación (`///`) que usan IntelliSense y Swagger.
- `screaming_case_constants`: constantes en mayúsculas separadas por `_` (`MiConstanteValorMaximo` → `MI_CONSTANTE_VALOR_MAXIMO`), con todas sus referencias.

## 0.8.3
- Renombrado: los nombres en mayúsculas se convierten al estilo de la regla (`MAX_VALUE` → `MaxValue`, `_maxValue`; `SCREAMCASE` → `Screamcase`; `URL` → `Url`). Las siglas de dos letras (`ID`, `IO`) se mantienen, como indican las guías de nomenclatura de .NET.

## 0.8.2
- Descripción de la extensión en español e inglés.
- Manual completo con imágenes y notas de versión incluidos en la extensión (enlaces *Introducción* y *Notas de la versión*).

## 0.8.1
- Icono e imagen de vista previa.
- Documentación: funcionalidades con imágenes y manual de uso, en español e inglés.

## 0.8.0
- `wrap_list_style`: parámetros y argumentos uno por línea (`one_per_line`) o todos los que quepan por línea (`fill`).
- `wrap_tolerance`: columnas que una línea puede pasar del límite antes de partirse.

## 0.7.2
- Después de guardar se comprueba en disco la codificación real del archivo y se registra en *Salida*.
- La codificación se aplica también a la configuración del búfer que usa Visual Studio al escribir el archivo.

## 0.7.1
- Si el archivo se abrió con una codificación incorrecta (muestra `�`), no se cambia la codificación y se avisa.

## 0.7.0
- `normalize_line_endings` y `normalize_charset`: `end_of_line` y `charset` del `.editorconfig` en cualquier archivo de texto.

## 0.6.0
- `wrap_attributes`: permitir que los atributos queden en una línea larga.
- `split_long_strings`: partir textos largos en trozos concatenados con `+`.
- Los operadores que quedan solos en una línea o en el lado contrario se mueven al lado configurado.

## 0.5.0
- `rename_symbols`, `rename_scope` y `rename_controller_actions`: renombrado según todas las reglas de nomenclatura
  (`Async`, prefijo `I`, constantes…), como Ctrl+R, R.

## 0.4.0
- Los valores propios de la extensión (`trailing_comma_style`, `binary_operator_new_line`, `guidelines`,
  `guideline_color`) pasan al `.codestraight`; el `.editorconfig` queda solo con claves estándar.

## 0.3.0
- `remove_extra_blank_lines`: como máximo una línea en blanco seguida y ninguna entre llaves de cierre.

## 0.2.0
- Se quitan las comas tras el último argumento (error de compilación) antes del resto de acciones.
- Usings repetidos, indentación de las líneas de continuación, listas de accesores en varias líneas.
- `explicit_types` y `log_level = verbose`.

## 0.1.0
- Primera versión: `.editorconfig` + `.codestraight`, renombrado de campos privados, namespace de archivo, usings,
  modificadores de acceso, formato, partición de líneas, coma final y guía vertical al guardar.

---

# English

## Unreleased
- `remove_braces`: remove the braces of `if`/`else`, `for`, `foreach` and `while` with a single simple statement, following `csharp_prefer_braces` of the `.editorconfig` (`false` or `when_multiline`). An `if`/`else` chain keeps the same style in every branch and the statement keeps its indentation (no mixing of tabs and spaces in files not yet migrated).

## 0.9.1
- Renaming: also parameters, locals, local functions and type parameters (`applicable_kinds = parameter`, `local`, `local_function`, `type_parameter`), with every reference.
- Renaming: a type parameter called just `T` follows the `T` prefix rule (it used to become `TT`).
- Renaming: when a rename reaches another symbol of the file (an interface method and its `async` implementation), the name is computed again and everything is settled in the same save (the `Async` suffix used to need a second save).
- Manual: team example with every naming rule and every `.codestraight` key explained (section 6.1 and the `docs/examples` folder).

## 0.9.0
- Status bar indicator: green when CodeStraight is active for the current document, grey when not, orange when the last save had an error. The tooltip shows the `.codestraight` file and the enabled actions; a click opens *Output → CodeStraight*.
- `remove_comments`: remove `//` and `/* */` comments, keeping the documentation comments (`///`) used by IntelliSense and Swagger.
- `screaming_case_constants`: constants in upper case separated by `_` (`MyConstantMaxValue` → `MY_CONSTANT_MAX_VALUE`), with every reference.

## 0.8.3
- Renaming: upper-case names are converted to the rule's style (`MAX_VALUE` → `MaxValue`, `_maxValue`; `SCREAMCASE` → `Screamcase`; `URL` → `Url`). Two-letter acronyms (`ID`, `IO`) are kept, as the .NET naming guidelines say.

## 0.8.2
- Extension description in Spanish and English.
- Full manual with images and release notes included in the extension (*Getting started* and *Release notes* links).

## 0.8.1
- Icon and preview image.
- Documentation: features with images and user manual, in Spanish and English.

## 0.8.0
- `wrap_list_style`: parameters and arguments one per line (`one_per_line`) or as many per line as fit (`fill`).
- `wrap_tolerance`: columns a line may go over the limit before it is wrapped.

## 0.7.2
- After saving, the real encoding of the file is checked on disk and written to *Output*.
- The encoding is also set on the buffer setting Visual Studio uses when writing the file.

## 0.7.1
- When a file was opened with the wrong encoding (it shows `�`), the encoding is not changed and a warning is logged.

## 0.7.0
- `normalize_line_endings` and `normalize_charset`: the `end_of_line` and `charset` of the `.editorconfig` on any text file.

## 0.6.0
- `wrap_attributes`: let attributes stay on a long line.
- `split_long_strings`: split long strings into parts concatenated with `+`.
- Operators left alone on a line or on the other side are moved to the configured side.

## 0.5.0
- `rename_symbols`, `rename_scope` and `rename_controller_actions`: renaming to every naming rule (`Async`, `I` prefix,
  constants…), like Ctrl+R, R.

## 0.4.0
- The extension's own values (`trailing_comma_style`, `binary_operator_new_line`, `guidelines`, `guideline_color`) move to
  `.codestraight`; `.editorconfig` keeps standard keys only.

## 0.3.0
- `remove_extra_blank_lines`: at most one blank line in a row and none between closing braces.

## 0.2.0
- Commas after the last argument (a compile error) are removed before the other actions.
- Repeated usings, continuation line indentation, multi-line accessor lists.
- `explicit_types` and `log_level = verbose`.

## 0.1.0
- First version: `.editorconfig` + `.codestraight`, private field renaming, file-scoped namespaces, usings, accessibility
  modifiers, formatting, line wrapping, trailing commas and a vertical guideline on save.
