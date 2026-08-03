DO $$ BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'easycob_api') THEN
        CREATE ROLE easycob_api LOGIN PASSWORD 'easycob-api-local';
    END IF;
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'easycob_worker') THEN
        CREATE ROLE easycob_worker LOGIN BYPASSRLS PASSWORD 'easycob-worker-local';
    END IF;
END $$;

GRANT CONNECT ON DATABASE easycob TO easycob_api, easycob_worker;
GRANT USAGE ON SCHEMA public TO easycob_api, easycob_worker;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO easycob_api, easycob_worker;
