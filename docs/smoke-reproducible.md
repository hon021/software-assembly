# Smoke frontend reproducible

Fecha: 2026-10-06  
Estado: plantilla, generador y validación local conformes; publicación por PR y ejecución alojada pendientes.

## Objetivo

Comprobar el perfil técnico desde una copia limpia, sin reutilizar una aplicación previa en `artifacts` ni cargar el material de referencia.

La [plantilla mínima](../tests/fixtures/frontend-smoke/package.json) y su lockfile se mantienen como código fuente. La aplicación es neutral: muestra un título y un contador para verificar compilación, renderizado e interacción. No contiene entidades o reglas de un negocio.

## Generación

```powershell
node scripts/materialize-frontend-smoke.mjs artifacts/repro-frontend
```

El generador comprueba versiones, package manager y overrides contra el perfil `2.0.2`, exige un lockfile, rechaza archivos generados o enlaces simbólicos en la plantilla y no sobrescribe un destino existente.

Para otra ejecución limpia se debe usar otro directorio vacío o retirar explícitamente la copia temporal previa después de conservar sus reportes. El generador no borra automáticamente archivos del operador.

## Validación local portable

Las tareas `Smoke repro: create`, `install`, `audit`, `lint`, `build`, `unit`, `browser` y `e2e` usan el Node y pnpm portables de `.tools`. Sus etapas también están expuestas por [run-frontend-smoke.mjs](../scripts/run-frontend-smoke.mjs).

El host compara versiones reales de Node y pnpm con el perfil y ejecuta procesos mediante argumentos, sin shell. Declara timeouts de cinco minutos, y diez para E2E. No es un sandbox del motor ni restringe por sí mismo permisos o red.

Resultados locales obtenidos sobre una copia nueva:

- Instalación estricta y congelada conforme.
- Auditoría sin vulnerabilidades conocidas reportadas.
- Lint y build de producción conformes.
- Dos pruebas unitarias conformes, incluyendo interacción.
- Una prueba Chromium conforme, sin reintentos automáticos.

JUnit unitario y E2E se guardan por separado en `test-results`; los artefactos del navegador usan un subdirectorio independiente. Playwright inicia y cierra su servidor Angular.

## GitHub Actions

El workflow propuesto incorpora tres jobs:

| Job | Responsabilidad |
|---|---|
| Motor checks | Pruebas y auditoría .NET y controles de repositorio. |
| Frontend smoke | Materializar, instalar con lockfile congelado, auditar, lintar, compilar, probar y ejecutar Chromium. |
| Motor CI | Check agregado obligatorio: exige que los dos jobs anteriores terminen con success. |

El nombre `Motor CI` se conserva para la protección de main ya aplicada. Un frontend fallido u omitido impide aprobar el check agregado; no se añade un job meramente informativo que pueda fallar sin bloquear el merge.

El runner Ubuntu instala Chromium y sus dependencias del sistema; el entorno local utiliza el navegador portable existente. La configuración de Playwright no contiene rutas específicas de Windows.

Los reportes se publican como artefactos de Actions durante 14 días. El directorio generado sigue excluido de Git; solo se versionan plantilla, lockfile y automatización.

## Pendientes

- Publicar mediante rama y PR, sin push directo a main.
- Observar una ejecución alojada conforme desde el checkout del PR.
- Integrar el PR mediante el check obligatorio, sin relajar la protección.
- Realizar la prueba negativa de protección y completar aprobación operativa de fase 1.

Crear este smoke no equivale a generar una aplicación de negocio ni completa la fase 2.