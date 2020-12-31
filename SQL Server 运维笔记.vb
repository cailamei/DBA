1	sql server 异常处理
	1.	sql server 2012不能全部用到CPU的逻辑核心数
		1)	查询Sql Server 逻辑核数使用情况
			SELECT COUNT(1) FROM SYS.DM_OS_SCHEDULERS WHERE SCHEDULER_ID<255;
			--128
			SELECT COUNT(1) FROM SYS.DM_OS_SCHEDULERS WHERE SCHEDULER_ID<255 AND IS_ONLINE=1;
			--40
		2)	结论：
			1>	Microsoft SQL Server 2012 (SP1)Copyright (c) Microsoft Corporation Enterprise Edition (64-bit) 最多可以使用40 个逻辑cpu ，128-40=88 个cpu 会浪费;
			2>	如果想不浪费，需要使用Sql Server 另一个企业版本 SQL SERVER 2012 ENTERPRISE CORE  Edition;
			3>	使用动态视图 SELECT * FROM sys.dm_os_sys_info 可以查询 server 的配置和SQL Server 可以使用的资源；
	2.	web 报错（"Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached."）
		using (SqlConnection con = new SqlConnection(strCon)) 
		{
			using (SqlCommand cmd = new SqlCommand(strCmdText, con)) 
			{
				con.Open();
				using (SqlDataReader dr = cmd.ExecuteReader())
				 {
					  //do stuff;
					  dr.Close();
				 }
			 }
			 con.Close();
		}
		This seemed to fix my problem. DataReader.Close() was the nail that did it. It seems like MS 
		should change their recommendation since I've found it all over their site suggesting 
		not to use the try { } finally { con.Close(); } pattern. I didn't try this explicitly, 
		since the pattern is fairly pervasive throughout our entire db layer and wanted to find something closer.
2	SQL Server 重要视图或表
	1.	Sql Server CPU相关视图 
		1)	SELECT * FROM sys.dm_os_sys_info --查询SQL SERVER 占用OS 资源相关信息
			cpu_count 操作系统中逻辑CPU个数,此值不会为NULL。
			hyperthread_ratio 指一个物理处理器包公开的逻辑内核数与物理内核数的比,此值不会为NULL。
			physical_memory_kb 操作系统中物理内存量,此值不会为NULL。
			virtual_memory_kb 指用户模式进程可用的虚拟地址空间总量,此值不会为NULL。
			committed_kb 指内存管理器中的已提交内存 (KB), 不包括内存管理器中的保留内存,此值不会为NULL。
			committed_target_kb 指SQL Server 内存管理器可以占用的内存量 (KB),此值不会为NULL。
			visible_target_kb 同committed_target_kb 相同,此值不会为NULL
			tack_size_in_bytes 指SQL Server 创建的每个线程的调用堆栈的大小,此值不会为NULL。 --每个线程占2M
			max_workers_count 指可以创建的最大工作线程数,此值不会为NULL。 与CPU 有关，计算方法如下:
				逻辑cpu num					32 位计算机							64 位计算机
				<= 4 个处理器					256									512 
				> 4 个处理器和 < 64 个处理器	256 +（（逻辑 CPU 位数 - 4）* 8）		512 +（（逻辑 CPU 位数 - 4）* 16） 
				> 64 个处理器 				256 + ((逻辑 CPU 位数 - 4) * 32) 	512 + ((逻辑 CPU 位数 - 4) * 32) 

			scheduler_count 指SQL Server 进程中配置的用户计划程序数,此值不会为NULL。
			scheduler_total_count指SQL Server 中的计划程序总数,此值不会为NULL。
			sqlserver_start_time指SQL Server 上次启动时的日期及时间,此值不会为NULL。
		2)	SELECT * FROM sys.dm_exec_sessions; --查询所有的session
		3)	SELECT * FROM sys.dm_exec_connections; -- 查询用户进程的session;
		4)	SELECT * FROM sys.dm_exec_requests; --查询当前sqlserver引擎所有的活动进程
		5)	SELECT * FROM sys.sysprocesses； -- 查询所有的session;		
		6)	SELECT * FROM sys.dm_os_workers  -- 查询当前线程
		7)	SELECT * FROM sys.dm_os_threads
		8)	SELECT * FROM sys.dm_os_schedulers --查询cpu 使用情况 
3	SQL SERVER 优化配置
	1.	最大并行度优化
		exec sp_configure 'max degree of parallelism'--系统默认并行度  
		exec sp_configure 'cost threshold for parallelism' --并发阈值  
		exec sp_configure 'max worker threads'--系统最大工作线程数  
		exec sp_configure 'affinity mask' --CPU关联
		eg:
		EXECsp_configure'max degree of parallelism',8;
		EXECsp_configure'cost threshold for parallelism',10;
		GO
		RECONFIGUREWITHOVERRIDE;
		GO
4	SQL SERVER 相关运维SQL:
	1.	当前线程数  
		select COUNT(*) as 当前线程数 from sys.dm_os_workers   
	2.	非SQL server create的threads  
		select * from sys.dm_os_threads where started_by_sqlservr=0 --即scheduler_id > 255   
	3.	有task 等待worker去执行  
		select * from sys.dm_os_tasks where task_state='PENDING'  
	4.	实例累积的信号（线程/CPU）等待比例是否严重  
		SELECT CAST(100.0 * SUM(signal_wait_time_ms) / SUM (wait_time_ms) AS NUMERIC(20,2))  AS [%signal (cpu) waits],    
		CAST(100.0 * SUM(wait_time_ms - signal_wait_time_ms) / SUM (wait_time_ms) AS  NUMERIC(20,2)) AS [%resource waits]    
		FROM sys.dm_os_wait_stats WITH (NOLOCK) OPTION (RECOMPILE);   
	5.	SqlServer各等待类型的线程等待信息  
		SELECT TOP 20   
		wait_type,waiting_tasks_count ,wait_time_ms,signal_wait_time_ms   
		,wait_time_ms - signal_wait_time_ms AS resource_wait_time_ms   
		,CONVERT(NUMERIC(14,2),100.0 * wait_time_ms /SUM (wait_time_ms ) OVER( )) AS percent_total_waits   
		,CONVERT(NUMERIC(14,2),100.0 * signal_wait_time_ms /SUM (signal_wait_time_ms) OVER( )) AS percent_total_signal_waits   
		,CONVERT(NUMERIC(14,2),100.0 * ( wait_time_ms - signal_wait_time_ms )/SUM (wait_time_ms ) OVER( )) AS percent_total_resource_waits   
		FROM sys .dm_os_wait_stats  
		WHERE wait_time_ms > 0  
		ORDER BY percent_total_signal_waits DESC  
	6.	闩锁(latch)等待的信息  
		select top 20 latch_class,waiting_requests_count,wait_time_ms,max_wait_time_ms  
		from sys.dm_os_latch_stats  
		order by wait_time_ms desc  
  
	7.	缓存中最耗CPU的语句  
		select total_cpu_time,total_execution_count,number_of_statements,[text]   
		from (  
			select top 20    
			sum(qs.total_worker_time) as total_cpu_time,    
			sum(qs.execution_count) as total_execution_count,   
			count(*) as  number_of_statements,    
			qs.plan_handle    
			from sys.dm_exec_query_stats qs   
			group by qs.plan_handle   
			order by total_cpu_time desc  
		) eqs cross apply sys.dm_exec_sql_text(eqs.plan_handle) as est  
		order by total_cpu_time desc   
	8.	当前正在执行的语句  
		SELECT   
		der.[session_id],der.[blocking_session_id],  
		sp.lastwaittype,sp.hostname,sp.program_name,sp.loginame,  
		der.[start_time] AS '开始时间',  
		der.[status] AS '状态',  
		der.[command] AS '命令',  
		dest.[text] AS 'sql语句',   
		DB_NAME(der.[database_id]) AS '数据库名',  
		der.[wait_type] AS '等待资源类型',  
		der.[wait_time] AS '等待时间',  
		der.[wait_resource] AS '等待的资源',  
		der.[reads] AS '物理读次数',  
		der.[writes] AS '写次数',  
		der.[logical_reads] AS '逻辑读次数',  
		der.[row_count] AS '返回结果行数'  
		FROM sys.[dm_exec_requests] AS der   
		INNER JOIN master.dbo.sysprocesses AS sp on der.session_id=sp.spid  
		CROSS APPLY  sys.[dm_exec_sql_text](der.[sql_handle]) AS dest   
		WHERE [session_id]>50 AND session_id<>@@SPID AND DB_NAME(der.[database_id])='platform'    
		ORDER BY [cpu_time] DESC  
	9.	重建DB 所有索引
		USE My_Database; 
		DECLARE @name varchar(100)
		DECLARE authors_cursor CURSOR FOR  Select [name]   from sysobjects where xtype='u' order by id
		OPEN authors_cursor
		FETCH NEXT FROM authors_cursor  INTO @name
		WHILE @@FETCH_STATUS = 0  
		BEGIN    
		DBCC DBREINDEX (@name, '', 90)
		FETCH NEXT FROM authors_cursor     INTO @name
		ENDdeallocate authors_cursor