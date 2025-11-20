CREATE TABLE cb_chatbots (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name VARCHAR(255) NOT NULL,
    description TEXT NULL,
    meta JSON NOT NULL,
    initial_response_id VARCHAR(100) NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE cb_sessions (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    chatbot_id BIGINT UNSIGNED NOT NULL,
    conversation_id VARCHAR(100) NOT NULL,
    user_identity VARCHAR(255) NULL,
    title VARCHAR(255) NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY idx_sessions_chatbot_id (chatbot_id),
    KEY idx_sessions_conversation_id (conversation_id),
    KEY idx_sessions_user_identity (user_identity),
    CONSTRAINT fk_sessions_chatbot
        FOREIGN KEY (chatbot_id)
        REFERENCES cb_chatbots (id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE cb_messages (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    session_id BIGINT UNSIGNED NOT NULL,
    role ENUM('user', 'assistant', 'system') NOT NULL,
    content MEDIUMTEXT NOT NULL,
    response_id VARCHAR(100) NULL,
    parent_response_id VARCHAR(100) NULL,
    metadata JSON NULL,
    usage JSON NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY idx_messages_session_id (session_id),
    KEY idx_messages_response_id (response_id),
    KEY idx_messages_parent_response_id (parent_response_id),
    CONSTRAINT fk_messages_session
        FOREIGN KEY (session_id)
        REFERENCES cb_sessions (id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE cb_files (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    message_id BIGINT UNSIGNED NOT NULL,
    s3_key VARCHAR(1024) NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    mime_type VARCHAR(255) NULL,
    file_size BIGINT UNSIGNED NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY idx_files_message_id (message_id),
    KEY idx_files_s3_key (s3_key),
    CONSTRAINT fk_files_message
        FOREIGN KEY (message_id)
        REFERENCES cb_messages (id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
