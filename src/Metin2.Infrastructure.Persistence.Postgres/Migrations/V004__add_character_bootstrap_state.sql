ALTER TABLE characters
    ADD COLUMN experience BIGINT NOT NULL DEFAULT 0,
    ADD COLUMN gold BIGINT NOT NULL DEFAULT 0,
    ADD COLUMN available_status_points BIGINT NOT NULL DEFAULT 0;

ALTER TABLE characters
    ADD CONSTRAINT ck_characters_experience_uint32 CHECK (experience BETWEEN 0 AND 4294967295),
    ADD CONSTRAINT ck_characters_gold_uint32 CHECK (gold BETWEEN 0 AND 4294967295),
    ADD CONSTRAINT ck_characters_available_status_points_uint32 CHECK (available_status_points BETWEEN 0 AND 4294967295);
