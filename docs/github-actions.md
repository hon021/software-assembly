# CI en GitHub Actions

Fecha: 2026-10-07

Estado: CI de motor y frontend activo; PR #1 y PR #4 integrados con checks aprobados; protección de main verificada por API.

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

La suite local de controles tiene 24 pruebas conformes, incluidas tres de protección de rama; la auditoría local real no reportó avisos. La primera ejecución alojada usó las 21 pruebas disponibles en ese commit.

La [primera ejecución alojada](https://github.com/hon021/software-assembly/actions/runs/37531738965) del commit `13c778a` terminó con `success` el 2026-10-06. Todos los pasos de `Motor CI` fueron aprobados: controles, exclusiones, restore, build, tests, auditoría y publicación de evidencia. La protección se aplicó posteriormente mediante una operación administrativa autorizada, no como efecto del YAML.

## Activación y protección de main

- [x] Publicar el workflow y comprobar una ejecución conforme en GitHub Actions.
- [x] Proteger `main` para exigir PR y el status check `Motor CI`, y deshabilitar force push y borrado.
- [x] Verificar con un PR de prueba que un gate fallido impide completar el merge: [PR #3](https://github.com/hon021/software-assembly/pull/3) hizo fallar `Motor CI` y GitHub lo marcó como bloqueado; se cerró sin merge.

El archivo YAML no configura protección de rama. Esa configuración requiere permisos administrativos y una decisión explícita sobre revisores y bypass. Los cambios de administración no se consideran realizados por inferencia.

### Política aplicada

El 2026-10-06 se aplicó y releyó la protección mediante la API de GitHub con autorización del propietario:

- Pull request obligatorio y cero aprobaciones de otro usuario, porque actualmente hay un solo mantenedor.
- Check `Motor CI` obligatorio y vinculado al proveedor GitHub Actions.
- Rama al día con la base requerida antes de merge.
- Conversaciones resueltas antes de merge.
- Force push y borrado de main deshabilitados.
- Restricciones aplicadas también a administradores, sin habilitar bypass.

Las credenciales existentes de Git se utilizaron solo en memoria; no se imprimieron ni almacenaron en el proyecto. El script [configure-main-protection.mjs](../scripts/configure-main-protection.mjs) verifica el destino, permisos administrativos, check aprobado y respuesta final. No reemplaza automáticamente una protección preexistente.

La regla ya está aplicada. El [PR negativo #3](https://github.com/hon021/software-assembly/pull/3) rastreó un archivo de `artifacts/`, haciendo fallar el gate de exclusiones y el agregado `Motor CI`. GitHub marcó el PR como bloqueado; se cerró sin merge y se eliminó la rama temporal. La evidencia quedó en el [PR #4](https://github.com/hon021/software-assembly/pull/4), integrado en `main` como `7349346` con checks aprobados.

**No usar las tareas de commit y push directo a main para nuevos cambios.** El flujo será rama de trabajo, commit, push de esa rama, PR, CI y merge. Crear y publicar esas ramas y PRs requiere autorización explícita; no se efectuó como parte de la operación administrativa.

## Límites

El [PR #1](https://github.com/hon021/software-assembly/pull/1) incorporó el [smoke frontend reproducible](smoke-reproducible.md), con plantilla y lockfile propios y job independiente. `Motor CI` es el check agregado que exige éxito de motor y frontend. El PR se integró en `main` y sus seis checks fueron aprobados en [GitHub Actions](https://github.com/hon021/software-assembly/actions/runs/37537764839).

Un runner alojado ejecuta el job en un entorno efímero, pero no constituye el sandbox de generación de código del motor. La aprobación real de dominios y su almacenamiento protegido siguen pendientes.

La definición Azure Pipelines se conserva como alternativa no activa; no es consumida automáticamente por GitHub. La fase 1 no se declara cerrada solo por crear el workflow o proteger main. La aceptación operativa sigue pendiente.