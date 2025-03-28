INSERT INTO users(upn)
	SELECT distinct imports.user_name 
	FROM ${STAGING_TABLE_ACTIVITY} imports
	left join users on users.upn = imports.user_name
	where users.upn is null;

INSERT INTO event_file_ext([name])
	SELECT distinct imports.extension_name 
	FROM ${STAGING_TABLE_ACTIVITY} imports
	left join event_file_ext on event_file_ext.[name] = imports.extension_name
	where event_file_ext.[name] is null and imports.extension_name is not null and imports.extension_name != '';

INSERT INTO event_file_names([name])
	SELECT distinct imports.file_name 
	FROM ${STAGING_TABLE_ACTIVITY} imports
	left join event_file_names on event_file_names.[name] = imports.file_name
	where event_file_names.[name] is null and imports.file_name is not null and imports.file_name != '';

INSERT INTO event_operations([name])
	SELECT distinct imports.operation_name 
	FROM ${STAGING_TABLE_ACTIVITY} imports
	left join event_operations on event_operations.[name] = imports.operation_name
	where event_operations.[name] is null and imports.operation_name is not null;


-- Insert unique SharePoint/OneDrive URLs from staging (events with URLs not null and don't already exist)
INSERT INTO urls(full_url)
	SELECT distinct imports.object_id 
	FROM ${STAGING_TABLE_ACTIVITY} imports
	left join 
		urls on urls.full_url = imports.object_id
	where 
		imports.object_id is not null AND imports.object_id != '' AND
		(imports.workload = 'SharePoint' OR imports.workload = 'OneDrive') AND
		not exists(select top 1 full_url from urls where full_url = imports.object_id)

		

-- Insert unique Site URLs from staging (events with URLs not null and don't already exist)
INSERT INTO sites(url_base)
	SELECT distinct imports.url_base 
	FROM ${STAGING_TABLE_ACTIVITY} imports
	left join 
		sites on sites.url_base = imports.url_base
	where 
		imports.url_base is not null AND 
		not exists(select top 1 url_base from sites where url_base = imports.url_base)


-- Type names
INSERT INTO event_types(name)
	SELECT distinct imports.type_name 
	FROM ${STAGING_TABLE_ACTIVITY} imports
	left join event_types on event_types.name = imports.type_name
	where 
		event_types.name is null and 
		imports.type_name is not null 
		AND (imports.workload = 'SharePoint' OR imports.workload = 'OneDrive');


-- Insert common
insert into audit_events (id, user_id, operation_id, time_stamp)
	SELECT imports.[log_id]
		  ,users.id as userId
		  ,event_operations.id as opId
		  ,[time_stamp]
	  FROM ${STAGING_TABLE_ACTIVITY} imports
	  inner join users on users.upn = imports.user_name
	  inner join event_operations on event_operations.[name] = imports.operation_name


-- Insert SharePoint
insert into event_meta_sharepoint(event_id, file_name_id, file_extension_id, item_type_id, url_id)
	SELECT imports.[log_id]
		  ,event_file_names.id as fileNameId
		  ,event_file_ext.id as fileExtId
		  ,event_types.id as typeId
		  ,urls.id as urlId
	  FROM ${STAGING_TABLE_ACTIVITY} imports
	  left join event_file_names on event_file_names.[name] = imports.file_name
	  left join event_file_ext on event_file_ext.[name] = imports.extension_name
	  inner join urls on urls.full_url = imports.object_id
	  left join event_types on event_types.name = imports.type_name
	where (imports.workload = 'SharePoint' OR imports.workload = 'OneDrive')
