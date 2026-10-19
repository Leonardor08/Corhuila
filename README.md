# RutaSur · v1 (MVC sobre arquitectura en capas)

RutaSur es un sistema de **reservas de turismo regional del sur del Huila** (San Agustín, Isnos y el Desierto de la Tatacoa) con **cupos limitados por salida**. Esta es la **v1** del proyecto de aula de *Diseño y Patrones Arquitectónicos* (CORHUILA, 2026-B). Está construida con un patrón clásico: **MVC en la presentación sobre una arquitectura de tres capas**.

> Atributo de calidad principal: **cero sobreventa**. Una salida nunca confirma más personas que su cupo, aunque muchas personas reserven al mismo tiempo.

---

## Requisitos

| Herramienta | Versión |
|---|---|
| .NET SDK | 8.0 o superior (`dotnet --version`) |
| Navegador | Cualquiera moderno |

No necesita instalar un motor de base de datos. La v1 usa **SQLite**: el archivo `rutasur.db` se crea solo y se carga con datos de ejemplo la primera vez que arranca.

## Ejecución

```bash
git clone <URL-del-repositorio>
cd rutasur
dotnet restore
dotnet run --project src/RutaSur.Web
```

Abra **http://localhost:5080**.

| Ruta | Qué hace |
|---|---|
| `/` | Catálogo de destinos y experiencias |
| `/Experiencias/Detalle/{id}` | Salidas próximas con cupos y ocupación |
| `/Reservas/Crear?salidaId={id}` | Formulario de reserva |
| `/Reservas/Consultar` | Consultar o cancelar con código y documento |
| `/Operacion` | Panel del operador: ocupación, reservas por salida y nuevas salidas |

Para empezar desde cero, detenga la aplicación y borre `src/RutaSur.Web/rutasur.db*`.

## Pruebas

```bash
dotnet test
```

Las pruebas usan una base SQLite real y temporal. Recorren las capas de Negocio y Datos. Entre ellas está el escenario de calidad **"cero sobreventa"**: 50 solicitudes concurrentes contra una salida de 10 cupos, que deben dar exactamente 10 reservas confirmadas y 0 cupos restantes.

## Arquitectura

```
┌───────────────────────────────────────────────┐
│ Presentación · RutaSur.Web (ASP.NET Core MVC)│  Controladores, vistas Razor, modelos de vista
└───────────────────────┬───────────────────────┘
                        │ solo DTOs e interfaces de servicio
┌───────────────────────▼───────────────────────┐
│ Negocio · RutaSur.Negocio                     │  Reglas: cupos, validaciones, cancelación 24 h
└───────────────────────┬───────────────────────┘
                        │ interfaces de repositorio
┌───────────────────────▼───────────────────────┐
│ Datos · RutaSur.Datos (EF Core + SQLite)      │  DbContext, entidades, repositorios, semilla
└───────────────────────────────────────────────┘
```

**Regla de dependencias:** cada capa solo referencia a la inmediatamente inferior. Las referencias entre proyectos (`.csproj`) lo hacen cumplir al compilar: `RutaSur.Web` no puede ver EF Core ni `RutaSur.Datos`.

**Cómo se garantiza "cero sobreventa"** (`SalidaRepositorio.IntentarDescontarCuposAsync`):

```sql
UPDATE Salidas SET CuposDisponibles = CuposDisponibles - @n
WHERE Id = @id AND CuposDisponibles >= @n;   -- 1 fila = confirmada · 0 filas = sin cupo
```

El descuento y la inserción de la reserva ocurren en **la misma transacción**. Además, una restricción `CHECK (CuposDisponibles >= 0)` en la base de datos sirve como defensa en profundidad.

Los diagramas UML (componentes, clases y secuencia) están en [`docs/uml`](docs/uml): fuentes PlantUML e imágenes PNG.

## Estructura

```
rutasur/
├── RutaSur.sln
├── src/
│   ├── RutaSur.Web/        # Presentación (MVC)
│   ├── RutaSur.Negocio/    # Servicios, DTOs, reglas, excepciones
│   └── RutaSur.Datos/      # EF Core, entidades, repositorios, datos semilla
├── tests/RutaSur.Tests/    # xUnit (incluye prueba de concurrencia)
└── docs/uml/               # Diagramas
```

## Requisitos funcionales cubiertos

| RF | Descripción | Dónde |
|---|---|---|
| RF1 | Consultar destinos y experiencias | `HomeController` |
| RF2 | Ver salidas con cupos disponibles | `ExperienciasController` |
| RF3 | Reservar (1 a 10 personas, aceptación de la Ley 1581) sin sobreventa | `ReservasController.Crear` |
| RF4 | Consultar reserva con código y documento | `ReservasController.Consultar` |
| RF5 | Cancelar con 24 h de anticipación y liberar cupos | `ReservasController.Cancelar` |
| RF6 | Panel de ocupación por salida | `OperacionController.Index` |
| RF7 | Listar las reservas de una salida | `OperacionController.Reservas` |
| RF8 | Programar nuevas salidas | `OperacionController.ProgramarSalida` |

## Versionado

```bash
git tag v1
git push --tags
```

La etiqueta `v1` marca esta versión antes de empezar la v2 (moderna).

## Equipo

| Integrante | Aporte principal |
|---|---|
| Oscar Leonardo Rugeles Rodríguez | _(completar)_ |
| _(integrante 2)_ | _(completar)_ |
| _(integrante 3)_ | _(completar)_ |
