# Validación local del perfil técnico

Fecha: 2026-10-06  
Perfil actual: `dotnet-angular`, versión `2.0.2`  
Estado: compatibilidad y auditoría locales conformes; aceptación operativa pendiente.

## Entorno y alcance

Se eligió validar localmente con herramientas portables. El workspace no tenía Node.js, pnpm, Docker ni Azure CLI disponibles y no es todavía un repositorio Git propio. No se registró CI remoto ni se crearon recursos cloud.

Se descargó Node.js `22.16.0` desde su fuente oficial y se verificó SHA-256 contra el checksum de la distribución. pnpm `10.11.0` y Chromium se instalaron dentro de `.tools`, sin instalación global. Se utilizó el SDK .NET `10.0.301` existente.

La aplicación sintética está en `artifacts/profile-smoke`, sin dominio de negocio ni dependencias del material de referencia. `.tools`, `artifacts` y `Base` están excluidos del futuro repositorio mediante `.gitignore`.

Este ejercicio invoca herramientas directamente. No constituye un runner aislado del motor ni una aplicación de negocio generada por la ensambladora.

## Resultados

| Comprobación | Resultado | Evidencia |
|---|---|---|
| Pruebas del motor con el perfil corregido | Conforme: 99 pruebas, cero fallos y cero omitidas. | Ejecución local de la solución del motor. |
| Instalación frontend con peer dependencies estrictas | Conforme. | Instalación de paquetes exactos y generación de lockfile. |
| Instalación frontend congelada | Conforme. | `pnpm install --frozen-lockfile`. |
| Lint frontend | Conforme. | ESLint con cero warnings admitidos. |
| Build Angular de producción | Conforme. | Bundle generado en `frontend/dist/profile-smoke`. |
| Pruebas unitarias Angular | Conforme: 2 pruebas. | `frontend/test-results/unit.xml`. |
| Prueba Chromium | Conforme: 1 prueba, sin errores JavaScript observados. | `frontend/test-results/e2e.xml`. |
| Pruebas backend VSTest | Conforme: 2 pruebas. | Reporte TRX en `backend/TestResults`. |
| Restauración backend bloqueada | Conforme. | `dotnet restore Application.slnx --locked-mode`. |
| Auditoría frontend actual | Conforme: sin vulnerabilidades conocidas reportadas; código de salida 0. | `pnpm audit --audit-level=high`. |

La auditoría inicial del perfil `2.0.1` reportó **67 hallazgos: 6 bajos, 26 moderados, 33 altos y 2 críticos**. Tras actualizar Angular y Vitest quedaron dos hallazgos; la corrección acotada del SDK MCP eliminó el bloqueo restante. La auditoría final no reportó vulnerabilidades conocidas.

Estos resultados pertenecen al lockfile de este smoke test; no describen todas las aplicaciones futuras ni certifican la seguridad de Node.js o del SDK. Los dos tests backend y su TRX provienen del smoke anterior, cuyo código y dependencias backend no cambiaron; frontend y motor se verificaron nuevamente en este incremento.

## Correcciones e incidencias

### Compatibilidad de Vitest

Angular Build `21.0.0` declara Vitest `^4.0.8` como peer dependency. El perfil `2.0.0` fijaba `4.0.0`, por lo que no cumplía ese contrato.

Se publicó [el perfil 2.0.1](../profiles/dotnet-angular/2.0.1/profile.json), fijando Vitest `4.0.8`, sin editar versiones anteriores. Posteriormente se publicó [el perfil 2.0.2](../profiles/dotnet-angular/2.0.2/profile.json), con Angular `21.2.25`, Vitest `4.1.11` y herramientas de lint actualizadas. Ambas aplicaciones seleccionan ahora esa última referencia.

### Política de PowerShell y entorno

La política local impidió ejecutar el bootstrap `.ps1`. No se cambió la política ni se elevó el proceso. Las herramientas se prepararon mediante comandos explícitos y las comprobaciones se ejecutaron mediante tareas de proceso de VS Code con rutas y entorno definidos.

La ejecución inicial en el terminal presentó resultados mezclados y un fallo de arranque del worker de Vitest, con cero pruebas ejecutadas. Se registró como problema ambiental. La ejecución posterior en una tarea de proceso definida pasó las dos pruebas; no se interpretó el intento fallido como prueba conforme.

El [bootstrap](../scripts/initialize-local-tools.ps1) queda disponible para entornos que autoricen su ejecución. No se debe desactivar una política corporativa para usarlo.

### Navegador y reportes

La descarga de un componente headless encontró errores DNS. El smoke test utilizó el Chromium completo portable disponible, con `channel: chromium` y sin reintentos automáticos de tests.

Playwright limpiaba su directorio de salida y eliminaba el JUnit unitario. Se configuró `outputDir: test-results/browser-artifacts` y se repitió la comprobación. Ambos reportes permanecen después de ejecutar unitarias y E2E.

### Seguridad

Se encontraron avisos críticos en Vitest y Piscina, además de avisos altos en Angular y dependencias transitivas. Algunos ejemplos:

- Vitest: GHSA-5xrq-8626-4rwp.
- Piscina: GHSA-67c8-pqhq-4rmx.
- Angular Core: GHSA-prjf-86w9-mfqv.

No se ejecutaron fixes automáticos, no se ignoraron advisories y no se bajó el umbral del gate. Se fijaron versiones corregidas y el override `@angular/cli>@modelcontextprotocol/sdk: 1.31.0`, relacionado con GHSA-6qxp-vccf-f47h. Se regeneró el lockfile con peer dependencies estrictas y se repitieron instalación congelada, lint, build, unitarias y Chromium.

El gate local pasó con cero vulnerabilidades conocidas reportadas. El resolutor valida estructura y firma de dominio, no certifica por sí solo la aprobación de seguridad de cualquier aplicación. CI remoto, aislamiento y aceptación humana siguen siendo necesarios.

## Repetir las comprobaciones

El bootstrap y el scaffold temporal deben existir previamente. Las tareas de proceso de [VS Code](../.vscode/tasks.json) permiten ejecutar:

- `Profile smoke: frozen install`.
- `Profile smoke: unit tests`.
- `Profile smoke: E2E`.
- `Profile smoke: dependency audit`.
- `Profile smoke: lint` y `Profile smoke: build`.
- `Motor: tests`.

La tarea E2E gestiona el servidor Angular y lo cierra al terminar. No queda un servidor de desarrollo permanente como parte de esta validación.

Los archivos bajo `artifacts` son temporales y están ignorados por Git. Las tareas no regeneran el scaffold desde cero; su automatización reproducible en un runner sigue pendiente. Los reportes pueden cambiar al repetir comandos y deben conservarse externamente si se usan como evidencia formal.

## Próximos pasos

1. Crear un repositorio propio sin publicar el directorio de referencias ni herramientas y artefactos locales.
2. Registrar CI, configurar validación obligatoria de PRs y verificar el pipeline alojado.
3. Autorizar y verificar el entorno aislado y la regeneración del smoke test desde una copia limpia.
4. Configurar autoridades reales y permisos del registro y aceptar formalmente la fase 1.

No se da por completada la fase 1 ni se habilita producción con esta evidencia.