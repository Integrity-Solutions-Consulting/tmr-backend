-- Huella de los objetos administrados. Ejecutar con search_path = pg_catalog.
-- Excluye usuarios, ACL, datos, valores en uso de secuencias, historiales y las copias operativas.
WITH schemas AS (
    SELECT oid, nspname FROM pg_namespace
    WHERE nspname IN ('administracion','auditoria','autenticacion','inventario','inventory','notificacion','time_report')
), relations AS (
    SELECT c.*, n.nspname FROM pg_class c JOIN schemas n ON n.oid = c.relnamespace
    WHERE c.relname <> '__EFMigrationsHistory'
      AND NOT (n.nspname = 'administracion' AND c.relname IN
          ('bkp_emailcorporativo_20261105','bkp_emailcorporativo_carga_20261105','bkp_persona_email_20261105'))
), definitions(kind, definition) AS (
    SELECT 'tabla', jsonb_build_array(nspname, relname, relkind, relpersistence, relrowsecurity, relforcerowsecurity)
    FROM relations WHERE relkind IN ('r','p','v','m','f')
    UNION ALL
    SELECT 'columna', jsonb_build_array(c.nspname,c.relname,a.attname,format_type(a.atttypid,a.atttypmod),
           a.attnotnull,a.attidentity,a.attgenerated,a.attcollation::regcollation::text,pg_get_expr(d.adbin,d.adrelid))
    FROM relations c JOIN pg_attribute a ON a.attrelid=c.oid
    LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum
    WHERE c.relkind IN ('r','p','v','m','f') AND a.attnum>0 AND NOT a.attisdropped
    UNION ALL
    SELECT 'restriccion',jsonb_build_array(c.nspname,c.relname,k.conname,pg_get_constraintdef(k.oid),k.convalidated)
    FROM relations c JOIN pg_constraint k ON k.conrelid=c.oid WHERE k.contype <> 'n'
    UNION ALL
    SELECT 'indice',jsonb_build_array(c.nspname,c.relname,pg_get_indexdef(i.indexrelid),i.indisvalid)
    FROM relations c JOIN pg_index i ON i.indrelid=c.oid
    UNION ALL
    SELECT 'funcion', jsonb_build_array(n.nspname,p.proname,replace(pg_get_functiondef(p.oid),chr(13)||chr(10),chr(10)))
    FROM pg_proc p JOIN schemas n ON n.oid=p.pronamespace WHERE p.prokind IN ('f','p','w')
    UNION ALL
    SELECT 'trigger',jsonb_build_array(c.nspname,c.relname,t.tgenabled,pg_get_triggerdef(t.oid))
    FROM relations c JOIN pg_trigger t ON t.tgrelid=c.oid WHERE NOT t.tgisinternal
    UNION ALL
    SELECT 'enum',jsonb_build_array(n.nspname,t.typname,array_agg(e.enumlabel ORDER BY e.enumsortorder))
    FROM pg_type t JOIN schemas n ON n.oid=t.typnamespace JOIN pg_enum e ON e.enumtypid=t.oid
    GROUP BY n.nspname,t.typname
    UNION ALL
    SELECT 'secuencia',jsonb_build_array(c.nspname,c.relname,s.seqtypid::regtype::text,s.seqstart,s.seqincrement,
           s.seqmin,s.seqmax,s.seqcache,s.seqcycle,owner.relname,a.attname)
    FROM relations c JOIN pg_sequence s ON s.seqrelid=c.oid
    LEFT JOIN pg_depend d ON d.classid='pg_class'::regclass AND d.objid=c.oid AND d.deptype IN ('i','a')
    LEFT JOIN pg_class owner ON owner.oid=d.refobjid
    LEFT JOIN pg_attribute a ON a.attrelid=d.refobjid AND a.attnum=d.refobjsubid
    UNION ALL
    SELECT 'vista',jsonb_build_array(nspname,relname,pg_get_viewdef(oid)) FROM relations WHERE relkind IN ('v','m')
    UNION ALL
    SELECT 'politica',jsonb_build_array(c.nspname,c.relname,p.polname,p.polcmd,p.polpermissive,
        pg_get_expr(p.polqual,p.polrelid),pg_get_expr(p.polwithcheck,p.polrelid))
    FROM relations c JOIN pg_policy p ON p.polrelid=c.oid
)
SELECT kind, encode(sha256(convert_to(string_agg(definition::text,E'\n' ORDER BY definition::text COLLATE "C"),'UTF8')),'hex')
FROM definitions GROUP BY kind ORDER BY kind;
