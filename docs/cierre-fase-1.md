# Cierre de fase 1

Fecha: 2026-10-07

Estado: base y validación local conformes; cierre formal pendiente.

## Evidencia técnica disponible

| Entregable | Estado | Evidencia |
|---|---|---|
| Núcleo independiente y máquina de estados | Implementado y probado. | 99 pruebas de la solución del motor. |
| Configuración de dos dominios y un perfil común | Implementada y probada. | Aplicaciones sintéticas de comercio y logística. |
| Registro firmado y publicación sin sobrescritura | Implementado y probado localmente. | Pruebas de firmas, autoridades, contenido y pins. |
| Perfil backend/frontend corregido | Versión `2.0.2` publicada localmente. | Versiones anteriores conservadas; overrides auditables. |
| Validación funcional frontend | Conforme. | Lint, build, 2 unitarias y 1 Chromium. |
| Auditoría frontend | Conforme en el lockfile probado. | Sin vulnerabilidades conocidas reportadas. |
| Smoke reproducible | Plantilla, lockfile y generador conformes localmente; integrado en main. | [PR #1](https://github.com/hon021/software-assembly/pull/1) y [ejecución alojada](https://github.com/hon021/software-assembly/actions/runs/37537764839): seis checks aprobados. |
| CI del motor | Workflow activo; `Motor CI` exige éxito del motor y frontend. | [PR #1](https://github.com/hon021/software-assembly/pull/1) aprobó el smoke. La [prueba negativa #3](https://github.com/hon021/software-assembly/pull/3) hizo fallar `Motor CI` y GitHub marcó el PR como bloqueado; la evidencia quedó registrada por el [PR #4](https://github.com/hon021/software-assembly/pull/4), integrado en `7349346` con checks aprobados. |

El [informe de validación](validacion-local-perfil.md) conserva el historial de fallos y sus correcciones. Una prueba local conforme no equivale a una aprobación humana ni a un servicio desplegado.

## Pendientes que requieren decisiones o acceso

| Pendiente | Información o autorización necesaria | Evidencia de cierre |
|---|---|---|
| Entorno aislado | Runner autorizado, imagen o VM y límites de red, recursos y permisos. | Smoke regenerado desde copia limpia y prueba de aislamiento. |
| Autoridades reales | Identidades de responsables y claves públicas confiables con alcance por dominio. | Paquete real aprobado y firma verificada, sin claves privadas en el agente. |
| Almacenamiento protegido | Ubicación y responsable operativo; permisos separados de publicador e implementador. | Intentos no autorizados de modificación rechazados y recuperación documentada. |
| Aceptación de arquitectura | Responsable técnico y registro de aprobación de las decisiones propuestas. | Decisión explícita que referencia la versión revisada y la evidencia. |

El destino remoto es `https://github.com/hon021/software-assembly`. La exclusión del material de referencia, herramientas y artefactos temporales se verifica antes de publicar. No hay todavía un proveedor de aislamiento autorizado. Los paquetes de negocio disponibles son sintéticos. No se creará una identidad humana ficticia ni se marcará una aprobación como otorgada por inferencia.

No se solicitarán contraseñas, tokens o claves privadas en documentos o respuestas de chat. La autenticación deberá realizarse directamente en el entorno autorizado. Las claves públicas no sustituyen la verificación de identidad y permisos.

## Orden para terminar

1. Autorizar el runner o VM y ejecutar el smoke desde una copia limpia, sin material de referencia.
2. Configurar autoridades y almacenamiento reales y comprobar publicación y lectura protegidas.
3. Revisar y aprobar formalmente las decisiones y criterios de salida de fase 1.

No se habilita merge autónomo ni producción en este cierre. La fase 2 permanece pendiente de aceptación de fase 1.