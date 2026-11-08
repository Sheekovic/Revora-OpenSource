BEGIN;
SET LOCAL ROLE revora_migrator;

-- Owner-authorized source archive f5e88d2729306ca6946356a52b3964f9ddb245db9f1e40a0539e0e126ade3d63.
-- The content digest is over canonical sorted JSON containing only the five
-- reviewed map-1002/1004 portal rows and five map-1004 NPC placement rows.
INSERT INTO gameplay_reference.import_sources
    (source_id, archive_sha256, content_sha256, classification)
VALUES
    ('51650009-1002-1004-8000-000000000001',
     'f5e88d2729306ca6946356a52b3964f9ddb245db9f1e40a0539e0e126ade3d63',
     '16630c8952354e3bf1521e471c990275e83842e8d32ed1cb8de4fb930cec753b',
     'reviewed-reference-data')
ON CONFLICT DO NOTHING;

INSERT INTO gameplay_reference.portals
    (source_id,legacy_id,source_map,source_x,source_y,destination_map,destination_x,destination_y)
VALUES
    ('51650009-1002-1004-8000-000000000001',64,1004,51,73,1002,402,391),
    ('51650009-1002-1004-8000-000000000001',73,1002,401,387,1004,51,70),
    ('51650009-1002-1004-8000-000000000001',220,1002,400,387,1004,51,70),
    ('51650009-1002-1004-8000-000000000001',221,1002,402,387,1004,51,70),
    ('51650009-1002-1004-8000-000000000001',222,1002,403,387,1004,51,70)
ON CONFLICT DO NOTHING;

INSERT INTO gameplay_reference.npcs
    (source_id,legacy_uid,npc_type,flags,face,map_id,x,y,active_month,active_begin,active_end,
     destination_map,destination_x,destination_y,duration)
VALUES
    ('51650009-1002-1004-8000-000000000001',400,1657,2,10,1004,33,53,0,0,0,0,0,0,0),
    ('51650009-1002-1004-8000-000000000001',10000,120,2,6,1004,56,33,0,0,0,0,0,0,0),
    ('51650009-1002-1004-8000-000000000001',10001,166,2,8,1004,33,65,0,0,0,0,0,0,0),
    ('51650009-1002-1004-8000-000000000001',10022,60,2,5,1004,44,33,0,0,0,0,0,0,0),
    ('51650009-1002-1004-8000-000000000001',35500,8450,2,56,1004,66,33,0,0,0,0,0,0,0)
ON CONFLICT DO NOTHING;

DO $$
BEGIN
    IF (SELECT count(*) FROM gameplay_reference.portals
        WHERE source_id='51650009-1002-1004-8000-000000000001') <> 5 THEN
        RAISE EXCEPTION 'reviewed portal slice count mismatch';
    END IF;
    IF (SELECT count(*) FROM gameplay_reference.npcs
        WHERE source_id='51650009-1002-1004-8000-000000000001') <> 5 THEN
        RAISE EXCEPTION 'reviewed NPC slice count mismatch';
    END IF;
END $$;

COMMIT;
