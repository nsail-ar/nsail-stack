Guardar los archivos que la instalación sube en tu propio destino: una carpeta del servidor, o
tu propio bucket S3-compatible (AWS, Cloudflare R2, Backblaze, MinIO, ...), con tu propia factura
del proveedor. La pantalla es **Configuración > Integraciones > Almacenamiento**.

## Antes de empezar

Si vas a guardar en una **carpeta del servidor**, no necesitás nada de afuera.

Si vas a guardar en tu propio **bucket**, necesitás, ya creados en la consola de tu proveedor:
un bucket, y una clave de acceso con permiso de escribir, leer, listar y borrar objetos dentro de
él. Cada proveedor tiene su propia consola y la cambia sin avisar, así que esta guía no numera
sus pantallas — una vez adentro de la consola de tu proveedor, lo que buscás es la sección de
buckets (o "object storage") y la de claves de acceso (o "API keys" / "access keys").

## Configurar

1. Entrá a **Configuración > Integraciones > Almacenamiento**.
2. En **Proveedor**, elegí **Local folder** para una carpeta del servidor, o **S3-compatible** para tu propio bucket.
3. Completá **Contenedor** con el nombre de la carpeta (local) o del bucket (S3-compatible).
4. Si elegiste S3-compatible, completá además **Dirección del servicio** — la de tu proveedor, sin el nombre del bucket —, **Región** — dejala vacía salvo que tu proveedor la pida —, **Clave de acceso** y **Clave secreta**: los cuatro valores salen de la consola de tu proveedor, de donde lo dejaste en Antes de empezar.
5. Tocá **Guardar**.

**La Clave secreta es de sólo escritura**: después de guardarla el campo vuelve vacío y debajo
dice que ya hay una guardada, así que volver a guardar los otros campos la conserva. Sólo se
escribe para reemplazarla.

## Probar

Al pie de la pantalla, el botón **Probar** escribe un objeto chico contra lo que está
**guardado**, lo vuelve a leer, lo lista y lo borra. Si los cuatro pasos salen bien, avisa que el
destino respondió; si acabás de cambiar algo, guardalo primero — prueba lo guardado, no lo que
está tipeado sin guardar.

## Si la prueba falla

- **"Esta instalación todavía no guarda en ningún lado"**, debajo de **Proveedor**: no elegiste ninguno — elegí uno y completá Contenedor antes de guardar.
- **El objeto se escribió pero no se pudo volver a leer**: la clave de acceso puede escribir pero no leer en ese bucket — revisá los permisos de la clave en la consola de tu proveedor.
- **El objeto que se leyó no coincide con el que se escribió**: algo entre la escritura y la lectura alteró el contenido — a esto no lo arregla una clave distinta; es un destino que no se puede usar tal como está.
- **El objeto se escribió pero no aparece al listar el contenedor**: falta el permiso de listar — agregalo en la consola de tu proveedor.
- **No se pudo usar el destino de almacenamiento**, con el motivo de tu proveedor a continuación: un endpoint mal escrito, una clave vencida, un bucket que no existe con ese nombre — el motivo es el que contesta tu proveedor, no uno que invente NSail.

## Quién lo factura

Tu proveedor te factura el espacio que ocupás y las lecturas y escrituras que hacés, según su
propio plan. NSail no agrega ningún cargo por esto.

**Revisado el 2026-10-25.**
