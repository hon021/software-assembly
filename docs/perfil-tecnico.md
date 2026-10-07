# Perfil técnico backend/frontend

Fecha: 2026-10-06  
Estado: contrato, planificador, validación local y CI alojado conformes; aceptación operativa pendiente.

## Alcance

El perfil define cómo preparar y validar una aplicación sin conocer su negocio. Las aplicaciones de comercio y logística seleccionan el mismo perfil `dotnet-angular` en versión `2.0.2`.

El [perfil versionado](../profiles/dotnet-angular/2.0.2/profile.json) usa el contrato técnico `schemaVersion: 1.1`. Las versiones anteriores permanecen sin cambios para auditoría. La descriptiva no puede generar planes. La versión `2.0.0` tenía una incompatibilidad de Vitest; la `2.0.1` la corrigió, pero no superó la auditoría de seguridad. La versión actual incorpora la corrección de dependencias verificada localmente.

El [planificador](../src/SoftwareAssembly.Core/TechnicalProfilePlanner.cs) valida y ordena etapas. No instala paquetes, crea aplicaciones ni inicia procesos.

## Herramientas fijadas

| Herramienta | Versión del perfil |
|---|---|
| SDK .NET | 10.0.301 |
| Node.js | 22.16.0 |
| pnpm | 10.11.0 |
| Angular | 21.2.25 |
| TypeScript | 5.9.3 |
| Vitest | 4.1.11 |
| Playwright | 1.56.1 |
| ESLint | 10.12.0 |
| Configuración JavaScript de ESLint | 10.0.1 |
| TypeScript ESLint | 8.71.1 |

Estas versiones definen una configuración reproducible, no una certificación general de soporte o seguridad. El smoke test local ejecutó instalación congelada, lint, build, unitarias y Chromium; su auditoría no reportó vulnerabilidades conocidas. El smoke reproducible también se ejecuta en GitHub Actions, donde el check agregado exige éxito del motor y frontend. Cualquier actualización de herramientas requiere publicar otra versión del perfil y actualizar explícitamente la selección de la aplicación. La aceptación operativa sigue pendiente.

### Override de seguridad

El perfil declara `@angular/cli>@modelcontextprotocol/sdk` fijado a `1.31.0`, asociado a GHSA-6qxp-vccf-f47h. Se resuelve únicamente esa dependencia transitiva; no se ignora el advisory ni se rebaja la severidad del gate.

El plan incluye los overrides y rechaza versiones flotantes o entradas contradictorias para el mismo padre y paquete. El futuro scaffold deberá materializarlos en `pnpm.overrides` y generar un lockfile coherente. La biblioteca todavía no modifica manifiestos; el smoke test lo configura explícitamente.

El preflight exige versiones exactas. El host debe obtener las observaciones de herramientas reales y de los paquetes instalados, no de una respuesta del agente ni solamente de lo declarado en un manifiesto.

## Estructura esperada del repositorio objetivo

```text
backend/
  Application.slnx
  TestResults/
frontend/
  package.json
  pnpm-lock.yaml
  angular.json
  playwright.config.ts
  test-results/
```

Esta es la estructura del producto que se creará, no la del motor. Los directorios y archivos se declaran mediante el perfil; el planificador actual no comprueba su existencia física.

El scaffold futuro deberá incluir lockfiles NuGet y pnpm válidos. El comando backend `restore --locked-mode` presupone `packages.lock.json` y configuración de restauración reproducible. El backend presupone VSTest; un proyecto basado en otro runner requerirá otro contrato de comandos.

El frontend deberá configurar Angular CLI, el builder de pruebas unitarias y los reportes JUnit compatibles con los argumentos declarados. También deberá definir un script `lint` y sus dependencias fijadas por lockfile. Estos archivos y paquetes aún no se generan en este incremento.

## Etapas del plan

| Etapa | Dependencias | Resultado esperado |
|---|---|---|
| `backend-restore` | Ninguna. | Dependencias restauradas sin modificar el lockfile. |
| `backend-build` | Restauración backend. | Compilación Release. |
| `backend-test` | Build backend. | Pruebas y evidencia TRX en `backend/TestResults`. |
| `frontend-install` | Ninguna. | Instalación pnpm con lockfile congelado. |
| `frontend-lint` | Instalación frontend. | Validación mediante el script lint del producto. |
| `frontend-build` | Lint frontend. | Build Angular de producción. |
| `frontend-test` | Build frontend. | Pruebas unitarias y JUnit en `frontend/test-results`. |
| `browser-install` | Pruebas frontend. | Chromium disponible; solo si se requiere E2E. |
| `frontend-e2e` | Preparación de navegador y pruebas backend. | Playwright y JUnit; solo si se requiere E2E. |

El reporte E2E fija `PLAYWRIGHT_JUNIT_OUTPUT_FILE=test-results/e2e.xml`, relativo al directorio frontend. La configuración del producto deberá proporcionar el proyecto Chromium y levantar los servidores necesarios con su ciclo de limpieza. El `outputDir` de Playwright deberá ser un subdirectorio separado, por ejemplo `test-results/browser-artifacts`, para que su limpieza no elimine el JUnit unitario. Las bibliotecas nativas del navegador deberán existir en la imagen del runner; no se instalarán mediante elevación interactiva.

Cada etapa declara argumentos separados, directorio de trabajo y timeout entre 1 y 1800 segundos. El plan usa colecciones de solo lectura y un digest del perfil. El runner deberá hacer cumplir los timeouts y recolectar la evidencia; su declaración no significa que ya se hayan ejecutado.

## Validaciones implementadas

- Esquema estricto y versiones sin rangos, comodines o `latest`.
- Herramientas de comando limitadas a `dotnet`, `node` y `pnpm`.
- Ejecución declarada sin shell y con comprobación de versiones obligatoria.
- Rutas declaradas relativas, sin segmentos de escape, rutas absolutas o separadores dependientes del sistema operativo.
- Separación de directorios backend/frontend y referencias de archivos dentro de su componente.
- IDs de etapas únicos, referencias de dependencias conocidas y grafo sin ciclos.
- Etapas esenciales presentes exactamente una vez y vinculadas a sus prerrequisitos.
- Etapas obligatorias sin dependencia de etapas E2E opcionales.
- Directorios de evidencia requeridos para pruebas y contenidos en el componente correspondiente.
- Variables de entorno limitadas al destino explícito del reporte E2E, sin campos para secretos.
- Versiones observadas coincidentes con las fijadas por el perfil.

La validación de rutas es textual. No controla symlinks ni analiza el significado de todos los argumentos de cada herramienta. El perfil es configuración de confianza, no contenido editable por el implementador; su pin debe quedar protegido. El runner seguirá necesitando aislamiento y restricciones de acceso aunque no use shell.

## Uso de la biblioteca

```csharp
var planner = new TechnicalProfilePlanner(validator);
var plan = planner.Create(profile, includeE2e: contractRequiresE2e);
TechnicalProfilePlanner.VerifyToolchain(plan, observedVersions);
```

El resolutor de aplicaciones comprueba que el perfil seleccionado pueda generar un plan básico. Cuando el contrato exija E2E, el host deberá solicitar explícitamente el plan con E2E; no debe deducir ese requisito del dominio ni omitirlo por falta de navegador.

La comprobación de versiones es una API separada porque requiere observaciones del entorno. Las pruebas unitarias usan mapas sintéticos; no certifican una instalación de Angular o Node.js.

## Bloqueos principales

| Código | Causa |
|---|---|
| `PROFILE_NOT_EXECUTABLE` | Perfil histórico descriptivo. |
| `PROFILE_LAYOUT_INVALID` | Archivos o componentes declarados en ubicaciones inconsistentes. |
| `PROFILE_PATH_UNSAFE` | Segmentos de escape en una ruta relativa. |
| `PROFILE_STAGE_DUPLICATE` | IDs de etapas repetidos. |
| `PROFILE_DEPENDENCY_UNKNOWN` | Referencia a una etapa inexistente. |
| `PROFILE_DEPENDENCY_CYCLE` | Grafo de dependencias cíclico. |
| `PROFILE_OPTIONAL_DEPENDENCY` | Etapa obligatoria dependiente de E2E opcional. |
| `PROFILE_REQUIRED_STAGE_MISSING` | Etapa esencial ausente o duplicada por operación. |
| `PROFILE_REQUIRED_DEPENDENCY_MISSING` | Operación sin su prerrequisito. |
| `PROFILE_E2E_STAGE_MISSING` | Plan E2E solicitado sin preparación y pruebas previas. |
| `PROFILE_EVIDENCE_REQUIRED` | Pruebas sin ubicación de evidencia. |
| `PROFILE_TOOL_MISSING` | Herramienta no observada por el host. |
| `PROFILE_TOOL_VERSION_MISMATCH` | Versión observada distinta de la fijada. |
| `PROFILE_OVERRIDE_DUPLICATE` | Overrides contradictorios para un mismo padre y paquete. |

Los errores de estructura, rutas absolutas, herramientas desconocidas, versiones flotantes o campos prohibidos se rechazan con `SCHEMA_INVALID`. Una herramienta ausente es un fallo de entorno, no un motivo para regenerar código de negocio.

## Verificación y pendientes

```powershell
dotnet test tests/SoftwareAssembly.Core.Tests/SoftwareAssembly.Core.Tests.csproj --configuration Release --filter FullyQualifiedName~TechnicalProfilePlannerTests
```

Se comprobó compatibilidad mediante un scaffold temporal en Windows con herramientas portables, no mediante el runner del motor. Se conservaron reportes unitarios, E2E y TRX. El informe [Validación local](validacion-local-perfil.md) detalla resultados, incidencias y la resolución del bloqueo de seguridad.

Quedan pendientes la detección automática de herramientas, scaffold del producto por el motor, ejecución aislada y autorización de integraciones. La fase 1 sigue en curso. El repositorio GitHub propio ya fue creado, pero CI remoto no está activado. Véase [Cierre de fase 1](cierre-fase-1.md).