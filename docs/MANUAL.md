# CodeStraight — Manual de uso / User manual

> 🇪🇸 [Español](#español) · 🇺🇸 [English](#english) · ✨ [Funcionalidades / Features](../README.md)

---

# Español

## 1. Instalación

**Requisitos:** Windows con Visual Studio 2022 (17.8 o posterior) o Visual Studio 2026, con la carga de trabajo de C#.

1. Si las tenías, quita las herramientas que CodeStraight sustituye:
   - **Extensiones → Administrar extensiones → Instaladas**: desinstalar el formateador (por ejemplo **CSharpier**) y la
     extensión de guías (por ejemplo **Editor Guidelines**).
   - **Herramientas → Opciones → Editor de texto → Limpieza de código**: desmarcar **"Ejecutar el perfil de Limpieza de
     código al guardar"**.
2. Cierra Visual Studio.
3. Instala **CodeStraight** con doble clic en `CodeStraight.vsix` (aún no está publicada en el Marketplace; el `.vsix` se
   genera con `build.ps1`).
4. Abre Visual Studio y comprueba en **Extensiones → Administrar extensiones → Instaladas** que aparece la versión
   esperada.

**Actualizar:** desinstalar la versión anterior, cerrar Visual Studio e instalar la nueva.
**Desinstalar:** **Extensiones → Administrar extensiones → Instaladas → CodeStraight → Desinstalar**.

## 2. Dónde se configura

| Archivo | Ubicación | Contenido |
|---|---|---|
| `.codestraight` | Raíz del repositorio (junto al `.sln`). CodeStraight lo busca subiendo carpetas desde el archivo que se guarda. | Interruptores de cada funcionalidad y valores propios de la extensión. **Sin este archivo, CodeStraight no hace nada.** |
| `.editorconfig` | La misma carpeta que el `.codestraight`. | Valores estándar: indentación, límite de línea, estilo, nomenclatura, codificación. |
| `.gitattributes` *(recomendado)* | Raíz del repositorio. | Saltos de línea que entrega git en cada máquina; debe coincidir con `end_of_line`. |

Los cambios en estos archivos se aplican en el siguiente guardado, sin reiniciar Visual Studio.

## 3. Referencia del archivo `.codestraight`

Formato igual que el `.editorconfig`: `clave = valor`, comentarios con `#`. Cualquier interruptor ausente o distinto de
`true` queda desactivado.

### 3.1 Interruptores

| Clave | Por defecto | Qué hace |
|---|---|---|
| `format_on_save` | `false` | Interruptor general: ejecutar las acciones al guardar. |
| `rename_symbols` | `false` | Renombrar según las reglas `dotnet_naming_*`, con todas las referencias de la solución: tipos, métodos, propiedades, eventos, campos, parámetros, variables locales, funciones locales y genéricos. Las variables locales y funciones locales se renombran con cualquier `rename_scope`; los parámetros y genéricos siguen al miembro o tipo que los declara. No se renombran los parámetros de un `record` posicional (son también las propiedades), de lo que sobrescribe o implementa algo de fuera de la solución, ni de las acciones de los controllers; tampoco si el nombre nuevo ya aparece en el mismo miembro. |
| `screaming_case_constants` | `false` | Renombrar las constantes a mayúsculas separadas por `_` (`ValorMaximo` → `VALOR_MAXIMO`), con todas sus referencias. Tiene prioridad sobre la regla de constantes del `.editorconfig`, funciona sin `rename_symbols` y respeta `rename_scope`. |
| `namespace_declarations` | `false` | Convertir a namespace de archivo (C# 10+, archivos con un solo namespace). |
| `remove_unused_usings` | `false` | Quitar los `using` repetidos (siempre) y los no usados (si el archivo compila). |
| `sort_usings` | `false` | Ordenar los `using`: globales, normales, `static` y alias. |
| `accessibility_modifiers` | `false` | Añadir los modificadores de acceso que faltan. |
| `explicit_types` | `false` | Sustituir `var` por el tipo explícito. |
| `remove_comments` | `false` | Quitar los comentarios `//` y `/* */`. Se mantienen los de documentación (`///`, `/** */`, usados por IntelliSense y Swagger) y los que son instrucciones para herramientas (`// ReSharper disable…`, `// language=…`). |
| `remove_extra_blank_lines` | `false` | Quitar líneas en blanco sobrantes. |
| `remove_braces` | `false` | Quitar las llaves de `if`/`else`, `for`, `foreach` y `while` con una sola instrucción simple (`return`, `throw`, `break`, `continue`, una llamada o asignación…), según `csharp_prefer_braces`. Se mantienen si hay comentarios junto a las llaves, si la instrucción es una declaración u otro `if`/bucle (evita el *dangling else*) o si alguna rama de la cadena `if`/`else` las necesita. La instrucción conserva su indentación. |
| `format_document` | `false` | Formatear el documento e indentar las líneas de continuación. |
| `wrap_long_lines` | `false` | Partir las líneas que superan `max_line_length`. |
| `trailing_comma` | `false` | Quitar comas tras el último argumento y aplicar `trailing_comma_style`. |
| `normalize_line_endings` | `false` | Aplicar `end_of_line` a todos los saltos de línea, en cualquier archivo de texto. |
| `normalize_charset` | `false` | Guardar cualquier archivo de texto con el `charset` del `.editorconfig`. |
| `show_guideline` | `false` | Dibujar las guías verticales. |

### 3.2 Valores propios de CodeStraight

| Clave | Por defecto | Valores |
|---|---|---|
| `rename_scope` | `private` | `private`, `internal` (privado + interno) o `all`. |
| `rename_controller_actions` | `false` | `true` para renombrar también los métodos públicos de los controllers de ASP.NET. |
| `wrap_list_style` | `one_per_line` | `one_per_line` (un parámetro/argumento por línea) o `fill` (todos los que caben por línea). |
| `wrap_tolerance` | `0` | Columnas que una línea puede pasar de `max_line_length` antes de partirse. |
| `wrap_attributes` | `true` | `false`: no se parten las líneas dentro de atributos (`[SwaggerResponse(...)]`). |
| `split_long_strings` | `false` | `true`: partir textos largos en trozos concatenados con `+`. |
| `binary_operator_new_line` | `before` | `before`: la línea nueva empieza con el operador; `after`: lo deja al final de la anterior. |
| `trailing_comma_style` | — | `omit`, `include` u `omit_when_single_line`. |
| `guidelines` | — | Columnas de las guías: `120` o `80, 120`. |
| `guideline_color` | gris | `#RRGGBB`, `#AARRGGBB` (con transparencia) o un nombre (`Red`, `SteelBlue`…). |
| `log_level` | — | `verbose`: registrar en *Salida* el resultado de cada acción en cada guardado. |

## 4. Claves del `.editorconfig` que usa CodeStraight

| Clave | Uso |
|---|---|
| `indent_style`, `indent_size`, `tab_width` | Indentación del documento y ancho del tabulador al medir líneas. |
| `max_line_length` | Límite para partir líneas. |
| `end_of_line`, `charset` | Saltos de línea y codificación (`normalize_line_endings`, `normalize_charset`). |
| `csharp_style_namespace_declarations` | `file_scoped` activa la conversión de namespace. |
| `dotnet_sort_system_directives_first` | Orden de los `using`. |
| `dotnet_style_require_accessibility_modifiers` | `always` o `for_non_interface_members` activan los modificadores. |
| `dotnet_style_allow_multiple_blank_lines_experimental` | `false`: como máximo una línea en blanco seguida. |
| `csharp_style_allow_blank_lines_between_consecutive_braces_experimental` | `false`: ninguna línea en blanco entre `}`. |
| `csharp_prefer_braces` | Para `remove_braces`: `false` quita las llaves siempre que se pueda; `when_multiline`, solo si la cabecera y la instrucción caben en una línea cada una; `true` no quita ninguna. |
| `dotnet_naming_rule.*`, `dotnet_naming_symbols.*`, `dotnet_naming_style.*` | Reglas de nomenclatura para el renombrado. Gana la regla más específica. |

La severidad (`:warning`, `suggestion`…) no cambia lo que hace CodeStraight, salvo `none`, que desactiva la regla.

## 5. Orden de las acciones al guardar un `.cs`

1. Comas tras el último argumento (error de compilación).
2. Renombrado.
3. Namespace de archivo.
4. Quitar usings.
5. Ordenar usings.
6. Modificadores de acceso.
7. `var` → tipo explícito.
8. Líneas en blanco.
9. Quitar llaves.
10. Formato del documento e indentación de continuación.
11. Partir líneas largas (operadores, `fill`, textos largos).
12. Coma final.
13. Saltos de línea.
14. Codificación (al escribir el archivo) y comprobación en disco después de guardar.

En los archivos que no son C# (JSON, XML, Markdown…) solo se aplican los pasos 13 y 14.

## 6. Ejemplo completo

**`.editorconfig`**
```ini
root = true

[*]
charset = utf-8
end_of_line = crlf

[*.cs]
indent_style = tab
indent_size = 4
tab_width = 4
max_line_length = 120
dotnet_style_allow_multiple_blank_lines_experimental = false:warning
csharp_style_allow_blank_lines_between_consecutive_braces_experimental = false:warning
csharp_style_namespace_declarations = file_scoped:warning
dotnet_sort_system_directives_first = true
dotnet_style_require_accessibility_modifiers = for_non_interface_members:warning

dotnet_naming_rule.private_fields_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_underscore.style = underscore_camel
dotnet_naming_rule.private_fields_underscore.severity = warning
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.underscore_camel.capitalization = camel_case
dotnet_naming_style.underscore_camel.required_prefix = _
```

**`.codestraight`**
```ini
format_on_save = true

rename_symbols = true
rename_scope = all
namespace_declarations = true
remove_unused_usings = true
sort_usings = true
accessibility_modifiers = true
remove_extra_blank_lines = true
format_document = true
wrap_long_lines = true
wrap_list_style = fill
wrap_tolerance = 12
split_long_strings = true
trailing_comma = true
trailing_comma_style = omit
normalize_line_endings = true
normalize_charset = true

show_guideline = true
guidelines = 120
guideline_color = #70808080
```

**`.gitattributes`**
```gitattributes
* text=auto eol=crlf
```

### 6.1 Ejemplo de equipo: todas las reglas de nomenclatura

Tipos, métodos, funciones locales y propiedades en PascalCase; interfaces con `I`; métodos `async` con `Async`;
campos privados (también `static readonly`) en `_camelCase`; constantes en PascalCase; parámetros y variables locales
en camelCase; genéricos con `T`. En el `.codestraight`, cada clave lleva su explicación en la misma línea. Los dos
archivos están listos para copiar en la carpeta `docs/examples` del repositorio.

**`.editorconfig`**
```ini
root = true

[*.cs]
# Indentación
indent_style = tab
indent_size = 4
tab_width = 4
# Límite de línea
max_line_length = 120

# Líneas en blanco: como máximo una seguida (IDE2000) y ninguna entre llaves de cierre (IDE2002)
dotnet_style_allow_multiple_blank_lines_experimental = false:warning
csharp_style_allow_blank_lines_between_consecutive_braces_experimental = false:warning

# Namespaces y usings
csharp_style_namespace_declarations = file_scoped:warning
csharp_using_directive_placement = outside_namespace:warning
dotnet_sort_system_directives_first = true

# Modificadores de acceso obligatorios
dotnet_style_require_accessibility_modifiers = for_non_interface_members:warning

# Usings no utilizados
dotnet_diagnostic.IDE0005.severity = warning

# Al partir una expresión, el operador empieza la línea nueva (igual que binary_operator_new_line = before)
dotnet_style_operator_placement_when_wrapping = beginning_of_line

# Nomenclatura: métodos async terminan en Async (más específica que la de métodos)
dotnet_naming_rule.async_methods_end_in_async.symbols = async_methods
dotnet_naming_rule.async_methods_end_in_async.style = end_in_async
dotnet_naming_rule.async_methods_end_in_async.severity = warning
dotnet_naming_symbols.async_methods.applicable_kinds = method
dotnet_naming_symbols.async_methods.required_modifiers = async
dotnet_naming_style.end_in_async.capitalization = pascal_case
dotnet_naming_style.end_in_async.required_suffix = Async

# Nomenclatura: interfaces empiezan por I
dotnet_naming_rule.interfaces_begin_with_i.symbols = interfaces
dotnet_naming_rule.interfaces_begin_with_i.style = begin_with_i
dotnet_naming_rule.interfaces_begin_with_i.severity = warning
dotnet_naming_symbols.interfaces.applicable_kinds = interface
dotnet_naming_style.begin_with_i.capitalization = pascal_case
dotnet_naming_style.begin_with_i.required_prefix = I

# Nomenclatura: constantes en PascalCase (más específica que la de campos privados)
dotnet_naming_rule.constants_pascal.symbols = constant_fields
dotnet_naming_rule.constants_pascal.style = pascal
dotnet_naming_rule.constants_pascal.severity = suggestion
dotnet_naming_symbols.constant_fields.applicable_kinds = field
dotnet_naming_symbols.constant_fields.required_modifiers = const

dotnet_naming_style.pascal.capitalization = pascal_case

# Nomenclatura: campos privados en _camelCase, también los static readonly (_logger)
dotnet_naming_rule.private_fields_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_underscore.style = underscore_camel
dotnet_naming_rule.private_fields_underscore.severity = error
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.underscore_camel.capitalization = camel_case
dotnet_naming_style.underscore_camel.required_prefix = _

# Nomenclatura: tipos en PascalCase (las interfaces tienen su propia regla)
dotnet_naming_rule.types_pascal.symbols = types
dotnet_naming_rule.types_pascal.style = pascal
dotnet_naming_rule.types_pascal.severity = warning
dotnet_naming_symbols.types.applicable_kinds = class, struct, enum, delegate

# Nomenclatura: métodos, funciones locales, propiedades y eventos en PascalCase (la de async es más específica)
dotnet_naming_rule.members_pascal.symbols = members
dotnet_naming_rule.members_pascal.style = pascal
dotnet_naming_rule.members_pascal.severity = warning
dotnet_naming_symbols.members.applicable_kinds = method, local_function, property, event

# Nomenclatura: parámetros y variables locales en camelCase
dotnet_naming_rule.locals_camel.symbols = locals
dotnet_naming_rule.locals_camel.style = camel
dotnet_naming_rule.locals_camel.severity = warning
dotnet_naming_symbols.locals.applicable_kinds = parameter, local
dotnet_naming_style.camel.capitalization = camel_case

# Nomenclatura: genéricos empiezan por T (T, TKey, TEntity)
dotnet_naming_rule.type_parameters_t.symbols = type_parameters
dotnet_naming_rule.type_parameters_t.style = begin_with_t
dotnet_naming_rule.type_parameters_t.severity = warning
dotnet_naming_symbols.type_parameters.applicable_kinds = type_parameter
dotnet_naming_style.begin_with_t.capitalization = pascal_case
dotnet_naming_style.begin_with_t.required_prefix = T

# Proyectos de test con nombres separados por _ (Crear_CuandoNoExiste_Lanza): quitar el # de las dos líneas
# siguientes (y ajustar la carpeta) para que esos métodos no se conviertan en CrearCuandoNoExisteLanza.
# [tests/**.cs]
# dotnet_naming_rule.members_pascal.severity = none
```

**`.codestraight`**
```ini
# CodeStraight: funcionalidades activas al guardar un archivo .cs.
# Los valores estándar (límite de línea, tabulador, namespace, nomenclatura...) se leen del .editorconfig.
# Aquí solo van los interruptores y los valores que únicamente usa CodeStraight.
# Cualquier interruptor ausente o distinto de true queda desactivado.

# General
format_on_save = true # Interruptor general: sin él no se ejecuta ninguna acción al guardar.
log_level = verbose # Ventana Salida > CodeStraight: el resultado de cada acción en cada guardado (sin él, solo errores y totales).

# Nomenclatura (reglas dotnet_naming_* del .editorconfig)
rename_symbols = true # Renombrar como Ctrl+R, R: declaración y todos los usos en la solución.
rename_scope = all # Hasta dónde renombra: private | internal | all. Locales y funciones locales, siempre.
rename_controller_actions = false # No renombrar los métodos públicos de los controllers (ni sus parámetros): rutas y enlaces.
screaming_case_constants = false # Constantes en PascalCase según el .editorconfig; true las pasaría a MAX_VALOR.

# Namespaces, usings y modificadores
namespace_declarations = true # namespace X { } -> namespace X; (csharp_style_namespace_declarations).
remove_unused_usings = true # Quitar los using repetidos y los que no se usan (si el archivo compila).
sort_usings = true # Ordenar los using, System primero (dotnet_sort_system_directives_first).
accessibility_modifiers = true # Añadir private/internal donde falten (dotnet_style_require_accessibility_modifiers).

# Contenido
explicit_types = false # var -> tipo explícito: pendiente de aprobación.
remove_comments = false # Mantener los comentarios // y /* */.

# Formato
remove_extra_blank_lines = true # Como máximo una línea en blanco seguida y ninguna entre llaves de cierre.
remove_braces = false # Quitar llaves de if/else/for/foreach/while con una sola instrucción (necesita csharp_prefer_braces = false o when_multiline).
format_document = true # Formatear el documento e indentar las líneas de continuación.
wrap_long_lines = true # Partir las líneas que superan max_line_length (120).
wrap_list_style = one_per_line # Al partir parámetros o argumentos: uno por línea (fill: todos los que quepan).
wrap_tolerance = 0 # Columnas que una línea puede pasar de 120 antes de partirse.
wrap_attributes = false # Los atributos ([SwaggerResponse(...)]) nunca se parten, aunque pasen de 120.
split_long_strings = true # Textos que no caben: se parten en trozos concatenados con + (el valor no cambia).
binary_operator_new_line = before # El operador (+, &&, ||, ??) empieza la línea nueva; after lo deja al final.

# Coma final
trailing_comma = true # Quitar la coma tras el último argumento (error de compilación) y aplicar el estilo siguiente.
trailing_comma_style = omit # Sin coma final en inicializadores, enums y listas: omit | include | omit_when_single_line.

# Codificación y saltos de línea (necesitan charset y end_of_line en el .editorconfig, que no están definidos)
normalize_line_endings = false # No cambiar los saltos de línea.
normalize_charset = false # No cambiar la codificación de los archivos.

# Guía vertical en el editor
show_guideline = true # Dibujar las guías verticales.
guidelines = 120 # Columnas de las guías (varias: 80, 120); la misma que max_line_length.
guideline_color = #70808080 # #RRGGBB, #AARRGGBB (con transparencia) o un nombre (Red, SteelBlue...).
```

## 7. Uso diario

- **Guardar** (`Ctrl+S`) aplica todo; no hay que ejecutar nada más.
- **Ver qué hizo:** **Ver → Salida** y, en el desplegable, **CodeStraight**.
- **Renombrados en otros archivos:** como con Ctrl+R, R, los demás archivos afectados quedan modificados sin guardar.
  Revisarlos y guardar todo (`Ctrl+Mayús+S`).
- **Migración gradual:** cada archivo adopta el formato la primera vez que se guarda. En archivos con muchos cambios,
  conviene un commit solo de formato antes del cambio funcional; en GitLab/GitHub, "Ocultar cambios de espacios en
  blanco" esconde la conversión de espacios a tabuladores.
- **Scripts de PowerShell:** Windows PowerShell 5.1 solo lee un script como UTF-8 si tiene BOM. Si el repositorio
  tiene `.ps1`, añade una excepción:
  ```ini
  [*.{ps1,psm1,psd1}]
  charset = utf-8-bom
  ```

## 8. Limitaciones

- Solo se lee el `.editorconfig` de la carpeta del `.codestraight`; los `.editorconfig` de subcarpetas no se tienen en cuenta.
- No se parten comentarios (por ejemplo, `/// <param …>` largos).
- `split_long_strings` no parte textos interpolados (`$"…"`) ni raw (`"""…"""`), ni palabras más largas que el límite.
- La conversión de namespace solo se hace en archivos con un único namespace.
- Con `rename_scope = all`, el código de otros repositorios que use nombres públicos no se actualiza.

## 9. Solución de problemas

| Síntoma | Qué revisar |
|---|---|
| No pasa nada al guardar | Que exista `.codestraight` con `format_on_save = true` y que la extensión aparezca instalada. Se activa al abrir una solución. |
| No aparece **CodeStraight** en *Salida* | La extensión no cargó: abrir una solución y comprobar la versión instalada. |
| Una acción no se aplica | Con `log_level = verbose`, mirar su línea en *Salida* (`no changes`, `ERROR …`). |
| Quedan usings sin usar | El archivo tiene errores de compilación; corregirlos y volver a guardar. |
| `normalize_charset: WARNING … '�'` | El archivo se abrió con una codificación incorrecta. Cerrar **sin guardar** y reabrirlo con la codificación correcta (**Archivo → Abrir con… → Editor con codificación**). |
| `Skipped …: took longer than 10 s` | Archivo muy grande o solución cargándose; volver a guardar cuando Visual Studio termine de cargar. |
| La guía no aparece | `show_guideline = true` y `guidelines` con una columna; cambiar de pestaña para recargar. |

## 10. Para quien mantiene la extensión

- **Compilar:** `build.ps1` en la raíz del repositorio (limpia, ejecuta los tests, genera el `.vsix` y comprueba que la
  versión empaquetada es la del manifiesto). La versión se cambia en `source.extension.vsixmanifest` y en
  `CodeStraightPackage.cs`.
- **Estructura:** `src/CodeStraight.Core` (toda la lógica, sin dependencias de Visual Studio, con tests en
  `tests/CodeStraight.Core.Tests`) y `src/CodeStraight.Vsix` (integración con Visual Studio: guardado, guía, ventana
  *Salida*).

---

# English

## 1. Installation

**Requirements:** Windows with Visual Studio 2022 (17.8 or later) or Visual Studio 2026, with the C# workload.

1. If you have them, remove the tools CodeStraight replaces:
   - **Extensions → Manage Extensions → Installed**: uninstall the formatter (for example **CSharpier**) and the
     guideline extension (for example **Editor Guidelines**).
   - **Tools → Options → Text Editor → Code Cleanup**: clear **"Run Code Cleanup profile on Save"**.
2. Close Visual Studio.
3. Install **CodeStraight** by double-clicking `CodeStraight.vsix` (it is not on the Marketplace yet; `build.ps1` builds
   the `.vsix`).
4. Open Visual Studio and check in **Extensions → Manage Extensions → Installed** that the expected version is listed.

**Update:** uninstall the previous version, close Visual Studio and install the new one.
**Uninstall:** **Extensions → Manage Extensions → Installed → CodeStraight → Uninstall**.

## 2. Where it is configured

| File | Location | Content |
|---|---|---|
| `.codestraight` | Repository root (next to the `.sln`). CodeStraight finds it by walking up from the saved file. | Feature switches and the extension's own values. **Without this file, CodeStraight does nothing.** |
| `.editorconfig` | Same folder as `.codestraight`. | Standard values: indentation, line length, style, naming, encoding. |
| `.gitattributes` *(recommended)* | Repository root. | Line endings git delivers on every machine; must match `end_of_line`. |

Changes to these files apply on the next save, without restarting Visual Studio.

## 3. `.codestraight` reference

Same format as `.editorconfig`: `key = value`, comments with `#`. Any switch that is missing or not `true` is off.

### 3.1 Switches

| Key | Default | What it does |
|---|---|---|
| `format_on_save` | `false` | Master switch: run the actions on save. |
| `rename_symbols` | `false` | Rename to the `dotnet_naming_*` rules, with every reference in the solution: types, methods, properties, events, fields, parameters, locals, local functions and type parameters. Locals and local functions are renamed with any `rename_scope`; parameters and type parameters follow the member or type that declares them. Parameters of positional `record`s (they are also the properties), of members that override or implement something outside the solution and of controller actions are not renamed, nor when the new name already appears in the same member. |
| `screaming_case_constants` | `false` | Rename constants to upper case separated by `_` (`MaxValue` → `MAX_VALUE`), with every reference. Takes precedence over the `.editorconfig` rule for constants, works without `rename_symbols` and follows `rename_scope`. |
| `namespace_declarations` | `false` | Convert to file-scoped namespaces (C# 10+, files with a single namespace). |
| `remove_unused_usings` | `false` | Remove repeated usings (always) and unused ones (when the file compiles). |
| `sort_usings` | `false` | Sort usings: global, regular, `static` and aliases. |
| `accessibility_modifiers` | `false` | Add missing accessibility modifiers. |
| `explicit_types` | `false` | Replace `var` with the explicit type. |
| `remove_comments` | `false` | Remove `//` and `/* */` comments. Documentation comments (`///`, `/** */`, used by IntelliSense and Swagger) and instructions for tools (`// ReSharper disable…`, `// language=…`) are kept. |
| `remove_extra_blank_lines` | `false` | Remove surplus blank lines. |
| `remove_braces` | `false` | Remove the braces of `if`/`else`, `for`, `foreach` and `while` with a single simple statement (`return`, `throw`, `break`, `continue`, a call or assignment…), following `csharp_prefer_braces`. They are kept when there are comments next to the braces, when the statement is a declaration or another `if`/loop (avoids the dangling else) or when any branch of the `if`/`else` chain needs them. The statement keeps its indentation. |
| `format_document` | `false` | Format the document and indent continuation lines. |
| `wrap_long_lines` | `false` | Wrap lines longer than `max_line_length`. |
| `trailing_comma` | `false` | Remove commas after the last argument and apply `trailing_comma_style`. |
| `normalize_line_endings` | `false` | Apply `end_of_line` to every line break, in any text file. |
| `normalize_charset` | `false` | Save any text file with the `charset` of the `.editorconfig`. |
| `show_guideline` | `false` | Draw the vertical guidelines. |

### 3.2 CodeStraight-only values

| Key | Default | Values |
|---|---|---|
| `rename_scope` | `private` | `private`, `internal` (private + internal) or `all`. |
| `rename_controller_actions` | `false` | `true` also renames public methods of ASP.NET controllers. |
| `wrap_list_style` | `one_per_line` | `one_per_line` (one parameter/argument per line) or `fill` (as many per line as fit). |
| `wrap_tolerance` | `0` | Columns a line may go over `max_line_length` before it is wrapped. |
| `wrap_attributes` | `true` | `false`: lines inside attributes (`[SwaggerResponse(...)]`) are not wrapped. |
| `split_long_strings` | `false` | `true`: split long strings into parts concatenated with `+`. |
| `binary_operator_new_line` | `before` | `before`: the new line starts with the operator; `after`: it stays at the end of the previous one. |
| `trailing_comma_style` | — | `omit`, `include` or `omit_when_single_line`. |
| `guidelines` | — | Guideline columns: `120` or `80, 120`. |
| `guideline_color` | gray | `#RRGGBB`, `#AARRGGBB` (with transparency) or a name (`Red`, `SteelBlue`…). |
| `log_level` | — | `verbose`: write the result of every action to *Output* on every save. |

## 4. `.editorconfig` keys used by CodeStraight

| Key | Use |
|---|---|
| `indent_style`, `indent_size`, `tab_width` | Document indentation and tab width when measuring lines. |
| `max_line_length` | Wrapping limit. |
| `end_of_line`, `charset` | Line endings and encoding (`normalize_line_endings`, `normalize_charset`). |
| `csharp_style_namespace_declarations` | `file_scoped` enables the namespace conversion. |
| `dotnet_sort_system_directives_first` | Using order. |
| `dotnet_style_require_accessibility_modifiers` | `always` or `for_non_interface_members` enable the modifiers. |
| `dotnet_style_allow_multiple_blank_lines_experimental` | `false`: at most one blank line in a row. |
| `csharp_style_allow_blank_lines_between_consecutive_braces_experimental` | `false`: no blank lines between `}`. |
| `csharp_prefer_braces` | For `remove_braces`: `false` removes the braces wherever possible; `when_multiline`, only when the header and the statement fit on one line each; `true` removes none. |
| `dotnet_naming_rule.*`, `dotnet_naming_symbols.*`, `dotnet_naming_style.*` | Naming rules for renaming. The most specific rule wins. |

The severity (`:warning`, `suggestion`…) does not change what CodeStraight does, except `none`, which disables the rule.

## 5. Order of the actions when a `.cs` file is saved

1. Commas after the last argument (compile error).
2. Renaming.
3. File-scoped namespace.
4. Remove usings.
5. Sort usings.
6. Accessibility modifiers.
7. `var` → explicit type.
8. Blank lines.
9. Remove braces.
10. Document formatting and continuation indentation.
11. Long line wrapping (operators, `fill`, long strings).
12. Trailing commas.
13. Line endings.
14. Encoding (when the file is written) and check on disk after saving.

Files other than C# (JSON, XML, Markdown…) only get steps 13 and 14.

## 6. Complete example

See the [Spanish section](#6-ejemplo-completo): the three files are the same in any language.

Section 6.1 has a team example with every naming rule (types, members, private fields including `static
readonly`, constants, parameters, locals, type parameters) and every `.codestraight` key explained on its own line. Both
files are ready to copy in the `docs/examples` folder of the repository.

## 7. Day to day

- **Saving** (`Ctrl+S`) applies everything; there is nothing else to run.
- **See what it did:** **View → Output** and pick **CodeStraight** in the drop-down.
- **Renames in other files:** as with Ctrl+R, R, the other affected files are left modified and unsaved. Review them and
  save all (`Ctrl+Shift+S`).
- **Gradual migration:** each file adopts the format the first time it is saved. For files with many changes, make a
  formatting-only commit before the functional change; "Hide whitespace changes" in GitLab/GitHub hides the switch from
  spaces to tabs.
- **PowerShell scripts:** Windows PowerShell 5.1 only reads a script as UTF-8 when it has a BOM. If the repository has
  `.ps1` files, add an exception:
  ```ini
  [*.{ps1,psm1,psd1}]
  charset = utf-8-bom
  ```

## 8. Limitations

- Only the `.editorconfig` in the folder of `.codestraight` is read; `.editorconfig` files in subfolders are ignored.
- Comments are not wrapped (for example long `/// <param …>` lines).
- `split_long_strings` does not split interpolated (`$"…"`) or raw (`"""…"""`) strings, nor words longer than the limit.
- The namespace conversion only applies to files with a single namespace.
- With `rename_scope = all`, code in other repositories that uses public names is not updated.

## 9. Troubleshooting

| Symptom | What to check |
|---|---|
| Nothing happens on save | A `.codestraight` file with `format_on_save = true` exists and the extension is installed. It activates when a solution is opened. |
| No **CodeStraight** in *Output* | The extension did not load: open a solution and check the installed version. |
| An action is not applied | With `log_level = verbose`, look at its line in *Output* (`no changes`, `ERROR …`). |
| Unused usings remain | The file has compile errors; fix them and save again. |
| `normalize_charset: WARNING … '�'` | The file was opened with the wrong encoding. Close it **without saving** and reopen it with the right encoding (**File → Open With… → Editor with Encoding**). |
| `Skipped …: took longer than 10 s` | Very large file or solution still loading; save again once Visual Studio finishes loading. |
| No guideline | `show_guideline = true` and `guidelines` with a column; switch tabs to reload. |

## 10. For maintainers of the extension

- **Build:** `build.ps1` at the repository root (cleans, runs the tests, produces the `.vsix` and checks that the
  packaged version matches the manifest). The version is set in `source.extension.vsixmanifest` and
  `CodeStraightPackage.cs`.
- **Structure:** `src/CodeStraight.Core` (all the logic, no Visual Studio dependencies, tested in
  `tests/CodeStraight.Core.Tests`) and `src/CodeStraight.Vsix` (Visual Studio integration: save, guideline, *Output*
  window).
