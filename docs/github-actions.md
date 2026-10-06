# CI en GitHub Actions

Fecha: 2026-10-06  
Estado: workflow definido y controles verificados localmente; ejecución alojada y protección de rama pendientes.

## Workflow

[Software Assembly CI](../.github/workflows/ci.yml) se ejecuta en cada push, pull request y solicitud manual. No utiliza filtros de rutas que puedan dejar un check obligatorio permanentemente pendiente.

El check estable es **Motor CI**. El job usa un runner Ubuntu alojado, SDK .NET `10.0.301`, Node.js `22.16.0` y un timeout de 15 minutos. Su token tiene permiso de lectura del contenido y el checkout no conserva credenciales en Git.

Las Actions se seleccionan mediante su versión mayor `v4`; son referencias actualizables del proveedor, no pins inmutables por SHA. Una política de suministro más estricta deberá fijar y mantener esos SHAs antes de exigir esa garantía.

## Gates

1. Ejecutar las pruebas de los controles CI con `node --test`.
2. Comprobar que el índice Git no contenga `Base`, `.tools` o `artifacts`.
3. Restaurar y compilar la solución del motor en Release.
4. Ejecutar sus pruebas con evidencia TRX.
5. Auditar dependencias NuGet directas y transitivas.
6. Publicar reportes disponibles, incluso cuando un gate falle.

El parser de auditoría bloquea vulnerabilidades High/Critical, severidades desconocidas y reportes incompletos. Low/Moderate generan advertencias. Si el comando de auditoría no puede ejecutarse, el gate falla; la ausencia de reporte no equivale a seguridad aprobada.

Los reportes TRX y el JSON de auditoría se conservan como artefactos durante 14 días. No se publican en Git. Una ejecución sin reportes no queda aprobada por el hecho de que la tarea de subida continúe: el gate anterior conserva su fallo.

## Verificación local

Los controles están en [ci-checks.mjs](../scripts/ci-checks.mjs) y sus pruebas en [ci-checks.test.mjs](../scripts/ci-checks.test.mjs).

```powershell
node --test scripts/ci-checks.test.mjs
node scripts/ci-checks.mjs repository
node scripts/ci-checks.mjs audit-local artifacts/ci/dependency-audit.json
```

En Windows con herramientas portables se pueden usar las tareas `CI: control tests` y `CI: dependency audit`. La selección explícita del ejecutable .NET se realiza mediante `SOFTWARE_ASSEMBLY_DOTNET`; en el runner alojado se usa la instalación provista por setup-dotnet.

La suite de controles tiene 21 pruebas conformes y la auditoría local real no reportó avisos. Esto no sustituye ejecutar el workflow en GitHub; la primera ejecución alojada sigue pendiente.

## Activación y protección de main

1. Publicar el workflow en el repositorio GitHub con autorización de commit y push.
2. Comprobar en Actions una ejecución conforme del commit publicado.
3. Crear una regla para `main` en Settings, Rules, Rulesets, o mediante branch protection disponible para el repositorio.
4. Exigir pull request, al menos una revisión humana y el status check `Motor CI` antes de merge.
5. Impedir force push y borrado de la rama; restringir bypass según los responsables autorizados.
6. Verificar con un PR de prueba que un gate fallido impide completar el merge.

El archivo YAML no configura protección de rama. Esa configuración requiere permisos administrativos y una decisión explícita sobre revisores y bypass. Los cambios de administración no se consideran realizados por inferencia.

## Límites

Este workflow valida el motor, no ejecuta el frontend temporal que está excluido de Git. La regeneración del smoke desde una copia limpia y su integración como job independiente se mantienen pendientes.

Un runner alojado ejecuta el job en un entorno efímero, pero no constituye el sandbox de generación de código del motor. La aprobación real de dominios y su almacenamiento protegido siguen pendientes.

La definición Azure Pipelines se conserva como alternativa no activa; no es consumida automáticamente por GitHub. La fase 1 no se declara cerrada solo por crear este workflow.