
begin transaction 

CREATE TABLE [dbo].[copilot_agents](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[agent_id] [nvarchar](max) NOT NULL,
	[name] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK_copilot_agents] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE dbo.event_copilot_chats ADD
	agent_id int NULL
GO
ALTER TABLE dbo.event_copilot_chats SET (LOCK_ESCALATION = TABLE)
GO

ALTER TABLE [dbo].[event_copilot_chats]  WITH CHECK ADD  CONSTRAINT [FK_event_copilot_chats_copilot_agents_agent_id] FOREIGN KEY([agent_id])
REFERENCES [dbo].[copilot_agents] ([id])
GO

ALTER TABLE [dbo].[event_copilot_chats] CHECK CONSTRAINT [FK_event_copilot_chats_copilot_agents_agent_id]
GO

commit transaction
