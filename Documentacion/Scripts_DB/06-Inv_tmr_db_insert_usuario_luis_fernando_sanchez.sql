DO $$
DECLARE
    v_now                  timestamptz := now();
    v_ip                   text := '127.0.0.1';
    v_usuario_sistema      text := 'SYSTEM';

    v_email                text := lower('luis.sanchez@integritysolutions.com.ec');
    v_nombres              text := 'Luis Fernando';
    v_apellidos            text := 'Sanchez Cordova';
    v_numeroidentificacion text := '0925968570';
    v_tipopersona          text := 'NATURAL';
    v_password_hash        text := '$2a$12$UFDB3k8YBM7xlG.ZMt6PGe7fetHSiIKf3HxnJpgka1oA5e6brt4Y2';
    v_app_name             text := 'Integrity interno';
    v_app_description      text := 'Aplicacion interna de Integrity';
    v_app_url              text := NULL;

    v_persona_id_email     int;
    v_persona_id_doc       int;
    v_persona_id           int;
    v_usuario_id_email     int;
    v_usuario_id_persona   int;
    v_usuario_id           int;
    v_aplicacion_id        int;
    v_rol_id               int;
    v_rol_name             text;
    v_roles                text[] := ARRAY['Administrador', 'Lider', 'Colaborador'];
BEGIN
    PERFORM set_config('search_path', 'autenticacion, administracion, public', true);

    IF to_regclass('tbl_autenticacion_aplicacion') IS NULL THEN
        RAISE EXCEPTION 'No se encontro tbl_autenticacion_aplicacion. Ejecuta primero el deploy de la base o revisa el esquema.';
    END IF;

    IF to_regclass('tbl_administracion_persona') IS NULL THEN
        RAISE EXCEPTION 'No se encontro tbl_administracion_persona. Ejecuta primero el deploy de la base o revisa el esquema.';
    END IF;

    IF to_regclass('tbl_autenticacion_usuario') IS NULL THEN
        RAISE EXCEPTION 'No se encontro tbl_autenticacion_usuario. Ejecuta primero el deploy de la base o revisa el esquema.';
    END IF;

    IF to_regclass('tbl_autenticacion_password_historial') IS NULL THEN
        RAISE EXCEPTION 'No se encontro tbl_autenticacion_password_historial. Ejecuta primero el deploy de la base o revisa el esquema.';
    END IF;

    IF to_regclass('tbl_autenticacion_rol') IS NULL THEN
        RAISE EXCEPTION 'No se encontro tbl_autenticacion_rol. Ejecuta primero el deploy de la base o revisa el esquema.';
    END IF;

    IF to_regclass('tbl_autenticacion_usuario_rol') IS NULL THEN
        RAISE EXCEPTION 'No se encontro tbl_autenticacion_usuario_rol. Ejecuta primero el deploy de la base o revisa el esquema.';
    END IF;

    IF to_regclass('tbl_autenticacion_usuario_aplicacion') IS NULL THEN
        RAISE EXCEPTION 'No se encontro tbl_autenticacion_usuario_aplicacion. Ejecuta primero el deploy de la base o revisa el esquema.';
    END IF;

    SELECT id
      INTO v_aplicacion_id
      FROM tbl_autenticacion_aplicacion
     WHERE lower(nombreaplicacion) = v_app_name
     LIMIT 1;

    IF v_aplicacion_id IS NULL THEN
        INSERT INTO tbl_autenticacion_aplicacion
            (nombreaplicacion, descripcion, urlbase, activo, usuariocreacion, fechacreacion, ipcreacion)
        VALUES
            (v_app_name, v_app_description, v_app_url, TRUE, v_usuario_sistema, v_now, v_ip)
        RETURNING id INTO v_aplicacion_id;
    END IF;

    SELECT id INTO v_persona_id_email
      FROM tbl_administracion_persona
     WHERE lower(email) = v_email
     LIMIT 1;

    SELECT id INTO v_persona_id_doc
      FROM tbl_administracion_persona
     WHERE numeroidentificacion = v_numeroidentificacion
     LIMIT 1;

    IF v_persona_id_email IS NOT NULL
       AND v_persona_id_doc IS NOT NULL
       AND v_persona_id_email <> v_persona_id_doc THEN
        RAISE EXCEPTION
            'Conflicto de datos: el correo % y la identificacion % pertenecen a personas distintas.',
            v_email, v_numeroidentificacion;
    END IF;

    v_persona_id := COALESCE(v_persona_id_email, v_persona_id_doc);

    IF v_persona_id IS NULL THEN
        INSERT INTO tbl_administracion_persona
            (numeroidentificacion, idtipoidentificacion, idgenero, idnacionalidad, tipopersona,
             nombres, apellidos, fechanacimiento, email, telefono, direccion, activo,
             usuariocreacion, fechacreacion, ipcreacion)
        VALUES
            (v_numeroidentificacion, NULL, NULL, NULL, v_tipopersona,
             v_nombres, v_apellidos, NULL, v_email, NULL, NULL, TRUE,
             v_usuario_sistema, v_now, v_ip)
        RETURNING id INTO v_persona_id;
    ELSE
        UPDATE tbl_administracion_persona
           SET numeroidentificacion = v_numeroidentificacion,
               tipopersona = v_tipopersona,
               nombres = v_nombres,
               apellidos = v_apellidos,
               email = v_email,
               activo = TRUE,
               usuariomodificacion = v_usuario_sistema,
               fechamodificacion = v_now,
               ipmodificacion = v_ip
         WHERE id = v_persona_id;
    END IF;

    SELECT id INTO v_usuario_id_email
      FROM tbl_autenticacion_usuario
     WHERE lower(email) = v_email
     LIMIT 1;

    SELECT id INTO v_usuario_id_persona
      FROM tbl_autenticacion_usuario
     WHERE idpersona = v_persona_id
     LIMIT 1;

    IF v_usuario_id_email IS NOT NULL
       AND v_usuario_id_persona IS NOT NULL
       AND v_usuario_id_email <> v_usuario_id_persona THEN
        RAISE EXCEPTION
            'Conflicto de autenticacion: el correo % y la persona % ya estan vinculados a usuarios distintos.',
            v_email, v_persona_id;
    END IF;

    v_usuario_id := COALESCE(v_usuario_id_email, v_usuario_id_persona);

    IF v_usuario_id IS NULL THEN
        INSERT INTO tbl_autenticacion_usuario
            (idpersona, email, hashpassword, ultimologin, emailverificado, intentosfallidos,
             bloqueadohasta, debecambiarpassword, activo, usuariocreacion, fechacreacion, ipcreacion)
        VALUES
            (v_persona_id, v_email, v_password_hash, NULL, TRUE, 0,
             NULL, FALSE, TRUE, v_usuario_sistema, v_now, v_ip)
        RETURNING id INTO v_usuario_id;
    ELSE
        UPDATE tbl_autenticacion_usuario
           SET idpersona = v_persona_id,
               hashpassword = v_password_hash,
               emailverificado = TRUE,
               intentosfallidos = 0,
               bloqueadohasta = NULL,
               debecambiarpassword = FALSE,
               activo = TRUE,
               usuariomodificacion = v_usuario_sistema,
               fechamodificacion = v_now,
               ipmodificacion = v_ip
         WHERE id = v_usuario_id;
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM tbl_autenticacion_password_historial
         WHERE idusuario = v_usuario_id
           AND hashpassword = v_password_hash
    ) THEN
        INSERT INTO tbl_autenticacion_password_historial
            (idusuario, hashpassword, fechacambio, activo, usuariocreacion, fechacreacion, ipcreacion)
        VALUES
            (v_usuario_id, v_password_hash, v_now, TRUE, v_usuario_sistema, v_now, v_ip);
    END IF;

    FOREACH v_rol_name IN ARRAY v_roles LOOP
        SELECT id
          INTO v_rol_id
          FROM tbl_autenticacion_rol
         WHERE nombre = v_rol_name
           AND activo = TRUE
         LIMIT 1;

        IF v_rol_id IS NULL THEN
            RAISE EXCEPTION 'No existe el rol "%" o no esta activo.', v_rol_name;
        END IF;

        IF NOT EXISTS (
            SELECT 1
              FROM tbl_autenticacion_usuario_rol
             WHERE idusuario = v_usuario_id
               AND idrol = v_rol_id
        ) THEN
            INSERT INTO tbl_autenticacion_usuario_rol
                (idusuario, idrol, asignadopor, asignadoen, fechaexpiracion, activo,
                 usuariocreacion, fechacreacion, ipcreacion)
            VALUES
                (v_usuario_id, v_rol_id, NULL, v_now, NULL, TRUE,
                 v_usuario_sistema, v_now, v_ip);
        END IF;
    END LOOP;

    IF NOT EXISTS (
        SELECT 1
          FROM tbl_autenticacion_usuario_aplicacion
         WHERE idusuario = v_usuario_id
           AND idaplicacion = v_aplicacion_id
    ) THEN
        INSERT INTO tbl_autenticacion_usuario_aplicacion
            (idusuario, idaplicacion, activo, usuariocreacion, fechacreacion, ipcreacion)
        VALUES
            (v_usuario_id, v_aplicacion_id, TRUE, v_usuario_sistema, v_now, v_ip);
    END IF;

    RAISE NOTICE 'Usuario % creado/actualizado correctamente con id=% y email=%',
        v_nombres || ' ' || v_apellidos, v_usuario_id, v_email;
END $$ LANGUAGE plpgsql;
