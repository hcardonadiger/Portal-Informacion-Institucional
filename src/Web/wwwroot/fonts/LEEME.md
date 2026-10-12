# Tipografías del portal

Estos `.woff2` los sirve el propio portal. **No se piden a Google Fonts**, y
no es una preferencia: la cabecera `Content-Security-Policy` de
`src/Web/Program.cs` declara `style-src 'self'` y `font-src 'self'`, de modo
que el navegador bloquea cualquier hoja o fuente de otro dominio.

Mientras el `<link>` apuntó a Google, el portal **nunca** renderizó Poppins:
la hoja se bloqueaba y todo caía al respaldo del sistema (Segoe UI) sin que
nada fallara. El único rastro era un error de consola. Si alguien vuelve a
poner un `<link>` a Google Fonts, pasará exactamente lo mismo.

## Qué hay aquí

| Archivo | Familia | Peso | Para qué |
|---|---|---|---|
| `Poppins-500.woff2` | Poppins | Medium | Bajada del header |
| `Poppins-800.woff2` | Poppins | ExtraBold | `h1`–`h3`, titulares, marca, login |
| `Manrope-400.woff2` | Manrope | Regular | Cuerpo del portal |
| `Manrope-500.woff2` | Manrope | Medium | Énfasis suave |
| `Manrope-600.woff2` | Manrope | SemiBold | Etiquetas, enlaces de pie |
| `Manrope-700.woff2` | Manrope | Bold | Encabezados de tabla, badges |
| `IBMPlexMono-400.woff2` | IBM Plex Mono | Regular | Códigos en listados |
| `IBMPlexMono-700.woff2` | IBM Plex Mono | Bold | Códigos destacados |

Son el subconjunto `latin`, que cubre el español completo: `á é í ó ú ñ ü ¿ ¡`
caen todos dentro de `U+00A1-00FF`. Pesan 141 KB en total.

Los pesos son **los que el portal usa**, medidos en pantalla, no supuestos.
Un peso que se pide y no se carga no falla: el navegador lo falsifica
engordando el trazo y se ve emborronado. Si cambia algún `font-weight`,
conviene volver a medir qué combinaciones familia/peso pide el portal antes
de dar por buena la lista.

Poppins trae solo Medium y ExtraBold porque son los dos cortes que el Manual
de Marca nombra (pág. 08) y el resto del portal ya no la usa.

## Licencias

Las tres familias se distribuyen bajo **SIL Open Font License 1.1**, que
permite la redistribución:

- Poppins — Indian Type Foundry. La licencia completa está en
  `src/Infrastructure/Reports/Fuentes/OFL.txt`, donde viajan las mismas
  Poppins embebidas para los PDF.
- Manrope — Mikhail Sharanda. <https://github.com/sharanda/manrope>
  Es la tipografía del portal Firmante (Trámites Digitales DIGER); se adopta
  aquí para que los dos sistemas de DIGER se lean como uno solo.
- IBM Plex Mono — IBM. <https://github.com/IBM/plex>

## Cómo actualizarlas

Pedir el CSS a Google con un navegador moderno como `User-Agent` (para que
devuelva `woff2`), quedarse con los bloques marcados `/* latin */` y bajar
cada `url(...)`. Las declaraciones `@font-face` viven en
`src/Web/wwwroot/css/fuentes.css` y hay que mantener el `unicode-range`.
