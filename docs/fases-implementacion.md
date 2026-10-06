# Fases de implementación de la ensambladora de software

Fecha: 2026-10-06  
Estado: fase 1 iniciada; aceptación pendiente  
Documento relacionado: [Propuesta de ensambladora de software](propuesta-ensambladora-software.md).

## 1. Objetivo y alcance

Implementar una plataforma que transforme historias aprobadas en contratos verificables, pruebas, cambios de código, PRs auditables y artefactos desplegables.

El motor será independiente del dominio de negocio. Cada aplicación seleccionará explícitamente un paquete de dominio, un perfil tecnológico y políticas de entrega versionados.

Este documento desarrolla las cuatro fases de la propuesta. No modifica su alcance. La fase 1 tiene un primer incremento implementado, cuyo alcance y pendientes se registran en [Arquitectura inicial](arquitectura-inicial.md); las fases siguientes permanecen pendientes. Las fechas, costos y asignaciones se definirán después de confirmar capacidad del equipo, proveedor de IA e infraestructura autorizada.

## 2. Restricciones transversales

- Los proyectos consultados serán únicamente referencia metodológica, sin dependencias de ejecución hacia `Base/`.
- No se trasladarán nombres, marcas, identificadores, namespaces, recursos corporativos ni activos propietarios de las referencias. Cualquier reutilización requerirá verificar permisos y licencias.
- El núcleo modelará entrega de software, no entidades de un negocio particular.
- Dominio, tecnología y políticas se configurarán por separado, sin dominio implícito o por defecto.
- La historia definirá el alcance; las reglas de negocio aplicables deberán estar aprobadas y tener procedencia verificable.
- El agente implementador no podrá modificar pruebas congeladas, contratos aprobados o políticas para hacer pasar una validación.
- El código generado se ejecutará en un entorno aislado, con permisos y recursos limitados.
- Un gate obligatorio fallido o no ejecutado impedirá avanzar.
- El MVP no realizará merge autónomo ni despliegue autónomo a producción.

## 3. Vista general

| Fase | Resultado principal | Dependencia | Estado |
|---|---|---|---|
| 1. Normalizar | Contratos de plataforma, configuración y estructura independiente. | Decisiones iniciales y responsables definidos. | En curso; aceptación pendiente |
| 2. MVP | Flujo completo hasta PR, con pilotos en dos negocios distintos. | Fase 1 aceptada. | Pendiente |
| 3. Operación | Recuperación, concurrencia y control de costos verificables. | Fase 2 aceptada. | Pendiente |
| 4. Escalar | Más proyectos y perfiles, con promoción controlada de artefactos. | Fase 3 aceptada. | Pendiente |

Los trabajos de una fase podrán solaparse si sus contratos están definidos, pero no se declarará completada hasta satisfacer todos sus criterios de salida.

## 4. Fase 1: Normalizar

### Objetivo

Definir una base genérica y coherente antes de automatizar la generación de aplicaciones.

### Orden de trabajo

1. Definir límites del motor, repositorios objetivo, integraciones autorizadas y alcance del perfil técnico inicial `.NET + Angular`.
2. Registrar decisiones arquitectónicas: orquestación durable, separación de runners, almacenamiento de evidencia y autenticación.
3. Diseñar una única máquina de estados con transiciones válidas, entradas, salidas y causas de bloqueo.
4. Definir esquemas versionados para paquetes de dominio, configuración de aplicación, perfiles técnicos, políticas y resultados de gates.
5. Definir aprobación y publicación de paquetes, resolución de versiones inmutables y cálculo de digests.
6. Crear la estructura independiente del proyecto y su pipeline de build y pruebas del propio motor.
7. Establecer pruebas de validación de esquemas, transiciones e independencia de las referencias.

### Entregables

- Decisiones arquitectónicas y contratos de componentes.
- Máquina de estados y catálogo de errores y bloqueos.
- Esquemas de configuración y ejemplos sintéticos de comercio y logística.
- Perfil tecnológico inicial y política de entrega inicial.
- Estructura de código y pruebas, sin dependencias hacia material de referencia.
- CI del motor con build, pruebas y revisión de dependencias.

### Criterios de salida

- Se valida una configuración que vincula aplicación, dominio, perfil y política sin mezclar sus responsabilidades.
- Se rechazan dominios ausentes, versiones inexistentes, paquetes no aprobados y referencias inválidas.
- Las transiciones válidas e inválidas de la máquina de estados tienen pruebas.
- El build y las pruebas del motor no requieren cargar `Base/`.
- Los documentos, configuraciones y código nuevos no contienen identificadores de los proyectos consultados.

### Fuera de alcance

Generación autónoma de aplicaciones, concurrencia de múltiples historias y promoción a producción.

## 5. Fase 2: MVP

### Objetivo

Completar una ejecución desde una historia aprobada hasta un PR verificable, con una historia activa por vez y revisión humana.

### Orden de trabajo

| Paso | Implementación | Evidencia requerida |
|---|---|---|
| 2.1. Configurar aplicación | Registrar repositorio, paquete aprobado, contextos, perfil técnico y política. | Configuración validada y versiones fijadas. |
| 2.2. Resolver contexto | Cargar solo información autorizada de la aplicación y excluir referencias y otros negocios. | Manifiesto de contexto con fuentes y digests. |
| 2.3. Admitir historia | Obtener requisitos y comentarios relevantes, comprobar calidad y detectar conflictos con reglas aprobadas. | Snapshot de fuentes y resultado de admisión. |
| 2.4. Construir contrato | Transformar criterios en escenarios y registrar reglas aplicables sin inventar requisitos. | Contrato trazable y mapa de fuentes. |
| 2.5. Generar pruebas | Crear pruebas y mapa escenario-test; comprobar aserciones y ejecutar contra el estado previo. | Resultado RED con fallos de comportamiento identificados. |
| 2.6. Proteger baseline | Conservar las pruebas y el contrato en almacenamiento o referencias que el implementador no pueda alterar. | Digests y baseline verificables de forma independiente. |
| 2.7. Implementar | Generar cambios dentro del alcance autorizado y ejecutar build y pruebas en sandbox. | Diff de implementación y resultados GREEN. |
| 2.8. Validar | Ejecutar gates aplicables, comprobar vigencia del contrato e integridad de pruebas y políticas. | Reportes de herramientas con estados explícitos. |
| 2.9. Refactorizar | Mejorar la implementación sin cambiar comportamiento ni pruebas congeladas. | Cambio separado y gates ejecutados nuevamente. |
| 2.10. Entregar | Publicar PR y evidencia; CI vuelve a verificar el cambio y produce el artefacto candidato. | PR asociado a la historia y artefacto identificado por commit. |

La evidencia RED debe demostrar fallos de comportamiento asociados al cambio solicitado. Un error de compilación, una dependencia ausente o cero pruebas ejecutadas no bastarán para declarar RED válido. Las regresiones ya existentes deberán distinguirse de los escenarios nuevos.

La generación de código dispondrá inicialmente de hasta tres intentos. Solo los defectos reparables de implementación consumirán ese presupuesto; errores de herramientas o infraestructura detendrán la ejecución con una causa explícita. El feedback se conservará de forma acumulativa.

### Entregables

- Orquestador y workers funcionales con entradas y salidas estructuradas.
- Adaptador inicial de historias y repositorios, y proveedor de IA intercambiable.
- Runner aislado con límites de tiempo, recursos, rutas modificables y accesos.
- Resolutor de dominio y registro reproducible del contexto utilizado.
- Validadores de trazabilidad, integridad, vigencia, arquitectura, cobertura y seguridad.
- Pruebas E2E cuando el contrato las requiera, con clasificación de defectos y fallos ambientales.
- Documentación de comportamiento derivada de resultados conformes, sin declarar probado lo que no se ejecutó.
- Dos pilotos sintéticos aprobados por sus responsables de negocio: comercio y logística.

### Criterios de salida

- Ambos pilotos llegan a un PR revisable usando el mismo motor y perfil técnico, sin modificar el núcleo entre negocios.
- Cada ejecución registra historia, commit, dominio, contextos, perfil, política, prompts, modelo y digests utilizados.
- El contexto de los workers no contiene contenido de otro negocio ni de los proyectos consultados.
- Un cambio del requisito relevante invalida el contrato y detiene la ejecución hasta reconstruir sus artefactos.
- La edición de pruebas, contrato o gates por el implementador es rechazada y detectada también en CI.
- Una contradicción entre historia y regla aprobada se bloquea antes de generar pruebas o código.
- Todos los gates obligatorios aplicables están aprobados; no se aceptan como conformes los estados no ejecutados.
- Los intentos agotados producen escalación y conservan evidencia, sin merge automático.
- El PR permite rastrear criterios de aceptación, escenarios, pruebas, cambios y resultados.

### Fuera de alcance

Procesamiento concurrente de múltiples historias, catálogo amplio de tecnologías y operación autónoma de producción.

## 6. Fase 3: Operación

### Objetivo

Hacer el flujo recuperable y seguro bajo fallos, solicitudes repetidas y ejecuciones concurrentes.

### Orden de trabajo

1. Incorporar cola de solicitudes y claves de idempotencia para iniciar ejecuciones y publicar resultados.
2. Implementar persistencia de checkpoints, reanudación y cancelación con versiones de contexto fijadas.
3. Agregar exclusión de trabajos incompatibles por repositorio y rama, y aislamiento entre aplicaciones.
4. Separar recuperación de fallos transitorios de regeneración por defectos de código, con límites independientes.
5. Medir consumo de tokens, costo, duración, intentos e intervención humana; detener ejecuciones que excedan presupuesto.
6. Publicar métricas y alertas, establecer retención de evidencia y procedimientos de soporte.
7. Verificar permisos mínimos y ejecutar pruebas de recuperación e aislamiento.

### Entregables

- Cola, persistencia de estado y control de concurrencia.
- Operaciones idempotentes para ramas, PRs y publicaciones de evidencia.
- Políticas de recuperación, cancelación, timeout y presupuesto.
- Observabilidad por ejecución y aplicación, sin exposición de secretos.
- Procedimientos de diagnóstico, escalación y recuperación.

### Criterios de salida

- Una solicitud duplicada no crea ejecuciones o PRs duplicados para la misma operación.
- Una interrupción permite reanudar desde el último checkpoint válido con el mismo contexto fijado.
- La cancelación impide nuevos efectos de entrega y conserva los resultados ya producidos.
- Dos trabajos incompatibles no escriben simultáneamente sobre la misma rama.
- La recuperación de un fallo ambiental no consume intentos de regeneración de código.
- Los límites de costo y tiempo detienen la ejecución y generan un reporte accionable.
- Las pruebas de aislamiento no permiten acceso a contexto, secretos o artefactos de otra aplicación.
- Una entrega parcialmente realizada se reconcilia antes de repetirse.

## 7. Fase 4: Escalar

### Objetivo

Incorporar aplicaciones y perfiles adicionales sin cambiar el núcleo, y promover artefactos con autorización y trazabilidad.

### Orden de trabajo

1. Formalizar el catálogo de perfiles técnicos y su suite de compatibilidad.
2. Incorporar un segundo perfil tecnológico y una nueva aplicación mediante configuración y adaptadores.
3. Versionar y publicar paquetes de dominio con validación, responsables y trazabilidad de aprobación.
4. Agregar mutation testing progresivamente, empezando por reglas de negocio críticas y umbrales acordados.
5. Automatizar la promoción del mismo artefacto entre ambientes, con autorizaciones y comprobaciones por destino.
6. Definir rollback, revocación de paquetes y manejo de versiones vulnerables sin alterar ejecuciones silenciosamente.

### Entregables

- Catálogo versionado de perfiles y paquetes, local o externo según necesidad operativa.
- Pruebas de compatibilidad para nuevos adaptadores y perfiles.
- Segundo perfil tecnológico y evidencia de incorporación de una aplicación adicional.
- Gates de mutación acordes con el riesgo y capacidad de ejecución.
- Pipeline de promoción y procedimientos de rollback.

### Criterios de salida

- Una nueva aplicación selecciona dominio y tecnología sin cambios en el núcleo del motor.
- Un perfil adicional supera la suite de compatibilidad y sus gates específicos.
- Ningún paquete de dominio puede relajar las políticas de seguridad de la plataforma.
- La identidad o digest del artefacto se mantiene entre ambientes; no se recompila para cada promoción.
- El despliegue a producción exige autorización explícita y resultados conformes.
- El rollback utiliza un artefacto conocido y conserva la trazabilidad de la operación.
- Un paquete revocado bloquea nuevas ejecuciones; las existentes requieren una decisión explícita y auditable.

## 8. Responsabilidades

| Rol | Responsabilidad |
|---|---|
| Responsable de producto | Priorizar alcance y aprobar criterios de aceptación de la plataforma. |
| Responsable de negocio de la aplicación | Aprobar paquete de dominio, reglas, historias y resolución de contradicciones. |
| Responsable técnico | Aprobar arquitectura, perfiles, políticas y cambios del núcleo. |
| Equipo de implementación | Construir componentes y mantener pruebas y documentación. |
| Responsable de calidad y seguridad | Revisar gates, aislamiento, permisos y evidencia de aceptación. |
| Operaciones y entrega | Autorizar infraestructura y promoción; mantener recuperación y rollback. |

Los roles podrán compartir personas, pero una respuesta generada por IA no sustituirá una aprobación requerida.

## 9. Evidencia y seguimiento

Cada fase deberá cerrar con entregables versionados, resultados de pruebas y aprobación registrada del responsable correspondiente. Un checklist marcado sin evidencia no constituye aceptación.

Se medirán tiempo hasta PR aceptable, aceptación al primer intento, costo por historia, intervención humana, defectos posteriores y tiempo de incorporación de dominios. El objetivo de contaminación de contexto entre aplicaciones será cero.

Los umbrales de cobertura, seguridad, mutación y operación se acordarán y versionarán como políticas. No se heredarán cifras de otro producto sin justificar su aplicabilidad.

## 10. Primer hito

Comenzar por la fase 1: aprobar los límites del motor y los esquemas de dominio, aplicación y perfil técnico. El primer hito verificable será validar dos configuraciones sintéticas de negocio con el mismo esquema y demostrar que el núcleo no depende de las referencias.

El primer incremento de fase 1 valida configuraciones sintéticas y transiciones; no ejecuta el flujo completo de generación. El plan no garantiza resultados legales sobre materiales de terceros. Su ejecución requiere las autorizaciones técnicas y de reutilización correspondientes.