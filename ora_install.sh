#!/bin/bash


unset http_proxy https_proxy

function green(){
    echo -e "\033[32m $1 \033[0m"
}

function red(){
    echo -e "\033[31m\033[01m\033[05m $1 \033[0m"
}

ipaddr=`ip a|awk '/global/{print substr($2,1,length($2)-3)}'`
host_name=`uname -n`
cat >>/etc/hosts <<EOF
$ipaddr    ${host_name}
EOF



# check OS version
checkos(){
[ `cat /etc/*release  |grep -i centos |grep -c "7\."` -gt 0 ] && OS_VER="CentOS7"
[ `cat /etc/*release  |grep -i centos |grep -c "6\."` -gt 0 ] && OS_VER="CentOS6"
}

checkos

# configure yum local repository
rm -rf /etc/yum.repos.d/*
if [ $OS_VER == "CentOS7" ];then
curl -o /etc/yum.repos.d/centos7.repo http://10.67.51.164/repofile/centos7.repo
fi

if [ $OS_VER == "CentOS6" ];then
curl -o /etc/yum.repos.d/centos6.repo http://10.67.51.164/repofile/centos6.repo
fi

sed -i '/^proxy/s/^proxy/#proxy/' /etc/yum.conf

# yum install prerequisite packages
green "starting install prerequisite packages for Oracle..."

rpm -ivh http://10.67.50.92/Tools/pdksh-5.2.14-37.el5.x86_64.rpm

yum install -y bc ntp wget xorg-x11-xauth unzip ftp gcc libaio libaio-devel compat-libstdc++-33 \
   glibc-devel glibc-headers gcc-c++ sysstat \
   elfutils-libelf-devel \
   xorg-x11-server-utils \
   rlwrap 

green "checking if there are missing packages..."
 rpm -q --qf '%{NAME}-%{VERSION}-%{RELEASE}(%{ARCH})\n' \
 ntp wget xorg-x11-xauth unzip ftp gcc libaio libaio-devel compat-libstdc++-33 \
 glibc-devel glibc-headers gcc-c++ sysstat \
 elfutils-libelf-devel xorg-x11-server-utils rlwrap
[ $? -gt 0 ] && red "there are missing packages " && exit 1


#check and config service like ntp,firewall/iptables/Selinux/timezone

#--------------stop & disable firewall service and setup timezone---------------
   
if [ $OS_VER == "CentOS7" ];then
timedatectl set-timezone 'Asia/Shanghai'
systemctl stop firewalld 
systemctl disable firewalld
fi

if [ $OS_VER == "CentOS6" ];then
/etc/init.d/iptables stop
chkconfig iptables off
ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime
fi

#---------------disable selinux-----------------
selinux_status=`getenforce`
if [ $selinux_status != "Disabled" ];then
  setenforce 0
  sed -i '/^SELINUX=/s/enforcing/disabled/' /etc/selinux/config
fi

#--------------- setup ntp service---------------- 
rpm -q chrony
ret=$?
if [ $ret -gt 0 ] && [ "$OS_VER" = "CentOS6" ];then
service ntpd stop
ntpdate 10.67.50.111
sed -i '/restrict ::1/a\server 10.67.50.111\nserver 10.191.131.131' /etc/ntp.conf
service ntpd start

elif [ $ret -gt 0 ] && [ "$OS_VER" = "CentOS7" ];then
systemctl stop ntpd
ntpdate 10.67.50.111
sed -i '/restrict ::1/a\server 10.67.50.111\nserver 10.191.131.131' /etc/ntp.conf
systemctl start ntpd

elif  [ $ret -eq 0 ];then
systemctl stop chronyd.service
systemctl disable chronyd.service
systemctl stop ntpd
ntpdate 10.67.50.111
sed -i '/restrict ::1/a\server 10.67.50.111\nserver 10.191.131.131' /etc/ntp.conf
systemctl start ntpd

else 
   echo "None of the condition met"
fi

# add Oracle groups
green "starting add groups for Oracle..."
for u in oinstall dba oper 
do
grep -q $u /etc/group
[ $? -gt 0 ] && groupadd $u
done

# add Oracle user
green "starting add Oracle user..."
useradd oracle -g oinstall -G dba,oper
echo "oracle:Foxconn123" | chpasswd
green "checking the group of user oracle..."
groups oracle


#  Download Oracle installation packages
if [ ! -d "/home/oracle/software" ]; then
mkdir -p /home/oracle/software
fi

green "Firstly, choose your Oracle version, please input the following options: 1  or  2  or 3"
green "you can choose: (1)11.2.2  (2)11.2.3  (3)11.2.4"
while :
do 
  read ora_ver
  case $ora_ver in
  1)
      ver=11.2.2
      wget -O /home/oracle/software/p10098816_112020_Linux-x86-64_1of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p10098816_112020_Linux-x86-64_1of7.zip
      wget -O /home/oracle/software/p10098816_112020_Linux-x86-64_2of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p10098816_112020_Linux-x86-64_2of7.zip
      break
      ;;
  2)  
      ver=11.2.3
      wget -O /home/oracle/software/p10404530_112030_Linux-x86-64_1of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p10404530_112030_Linux-x86-64_1of7.zip
      wget -O /home/oracle/software/p10404530_112030_Linux-x86-64_2of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p10404530_112030_Linux-x86-64_2of7.zip
      break
      ;;
  3)  
      ver=11.2.4
      wget -O /home/oracle/software/p13390677_112040_Linux-x86-64_1of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p13390677_112040_Linux-x86-64_1of7.zip
      wget -O /home/oracle/software/p13390677_112040_Linux-x86-64_2of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p13390677_112040_Linux-x86-64_2of7.zip
      break
      ;;
  *)
      echo "Your option is not valid. Please choose (1)11.2.2  (2)11.2.3   (3)11.2.4"
      ;;
  esac
done

#D_URL=http://10.67.50.92/Oracle/Oracle11G%20R2
#PCK1=p13390677_112040_Linux-x86-64_1of7.zip
#PCK2=p13390677_112040_Linux-x86-64_2of7.zip
#wget -O /home/oracle/$PCK1 ${D_URL}/$PCK1
#wget -O /home/oracle/$PCK2 ${D_URL}/$PCK2
(cd /home/oracle/software;ls *.zip |xargs -n1 unzip )
chown -R oracle:oinstall /home/oracle/software/database
mkdir -p /data
chown -R oracle:dba /data
#ver=`echo 11.2.${PCK1:14:1}`
mkdir -p /home/oracle/product/${ver}/dbhome_1
mkdir -p /home/oracle/oraInventory
chown -R oracle:dba /home/oracle
chmod -R 755 /home/oracle

# configure Oracle SID
green "Please input the Oracle SID: "
read sid
echo ${sid} |grep -qP '\W'
if [ $? -eq 0 ]; then
red "Don't input special character!!" && exit 1
fi
export ORACLE_BASE=/home/oracle
export ORACLE_HOME=/home/oracle/product/${ver}/dbhome_1
cat >>/home/oracle/.bash_profile << EOF
umask 022  
unset USERNAME 
export TMPDIR=/tmp
export ORACLE_BASE=/home/oracle
export ORACLE_SID=${sid}
export ORACLE_HOME=/home/oracle/product/${ver}/dbhome_1
export ORACLE_TERM=xterm 
export TNS_ADMIN=$ORACLE_HOME/network/admin
export LD_LIBRARY_PATH=$ORACLE_HOME/lib:/lib:/usr/lib:/usr/openwin/lib:/usr/local/lib
export PATH=$ORACLE_HOME/bin:$PATH
stty erase  ^H
alias sqlplus='rlwrap sqlplus'
alias rman='rlwrap rman'
EOF

# configure sysctl.conf
mem_shmmax=`free -b|awk '/Mem/{print $2}'`
mem_shmall=`expr $mem_shmmax / 4096`

cat >> /etc/sysctl.conf <<EOF
fs.aio-max-nr = 1048576 
fs.file-max = 6815744
kernel.shmall = ${mem_shmall}
kernel.shmmax = ${mem_shmmax}
kernel.shmmni = 4096
kernel.sem = 250 32000 100 128
net.ipv4.ip_local_port_range = 9000 65500
net.core.rmem_default = 1048576
net.core.rmem_max = 4194304
net.core.wmem_default = 1048576
net.core.wmem_max =  2621440
net.ipv4.tcp_wmem = 262144 262144 262144
net.ipv4.tcp_rmem = 4194304 4194304 4194304
EOF
sysctl -p /etc/sysctl.conf

# configure limits.conf
cat >> /etc/security/limits.conf <<EOF
oracle          soft     nofile   1024
oracle          hard    nofile   65536
oracle          soft     nproc   2047
oracle          hard    nproc   16384
EOF

# configure pam.d/login
cat >> /etc/pam.d/login <<EOF
#Oracle user
session required pam_limits.so
EOF

# customize db_install.rsp
source /home/oracle/.bash_profile
green "The next step will install Oracle software only, please key in 'y'  or 'n'"

while :
do
  read db_sw_install
  case $db_sw_install in
  y)
    green "continue install Oracle software..."
    break
      ;;
  n)
    green "the script will be quit now."
      exit 0

      ;;
  *)
      red "input string is invaild, please key in y or n"
      ;;
  esac
done

#  configure db_install.rsp file
sed -i 's/^oracle.install.option=/oracle.install.option=INSTALL_DB_SWONLY/' /home/oracle/software/database/response/db_install.rsp
host_name=`uname -n`; sed -i 's/^ORACLE_HOSTNAME=/ORACLE_HOSTNAME='${host_name}'/' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^UNIX_GROUP_NAME=/UNIX_GROUP_NAME=oinstall/' /home/oracle/software/database/response/db_install.rsp
sed -i 's#^INVENTORY_LOCATION=#INVENTORY_LOCATION=/home/oracle/oraInventory#' /home/oracle/software/database/response/db_install.rsp
#sed -i 's/^SELECTED_LANGUAGES=/SELECTED_LANGUAGES=en/' /home/oracle/software/database/response/db_install.rsp
sed -i 's#^ORACLE_HOME=#ORACLE_HOME='${ORACLE_HOME}'#' /home/oracle/software/database/response/db_install.rsp
sed -i 's#^ORACLE_BASE=#ORACLE_BASE='${ORACLE_BASE}'#' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^oracle.install.db.InstallEdition=/oracle.install.db.InstallEdition=EE/' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^oracle.install.db.DBA_GROUP=/oracle.install.db.DBA_GROUP=dba/' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^oracle.install.db.OPER_GROUP=/oracle.install.db.OPER_GROUP=oper/' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^DECLINE_SECURITY_UPDATES=/DECLINE_SECURITY_UPDATES=true/' /home/oracle/software/database/response/db_install.rsp

su - oracle -c "cd /home/oracle/software/database/;./runInstaller -silent -force -waitforcompletion  -responseFile /home/oracle/software/database/response/db_install.rsp -ignorePrereq"
sh /home/oracle/oraInventory/orainstRoot.sh
sh /home/oracle/product/11.2.4/dbhome_1/root.sh

green "Cheers, guys, Oracle is installed successfully"
#while :
#do
#sleep 10
#if test -f /home/oracle/oraInventory/orainstRoot.sh;then
#  sh /home/oracle/oraInventory/orainstRoot.sh
#fi
#if test -f /home/oracle/product/11.2.4/dbhome_1/root.sh;then
#  sh /home/oracle/product/11.2.4/dbhome_1/root.sh
#  break
#fi
#done

# run dbca silent install
green "starting install Oracle instance now..."

while :
do
green "Please type the password for user SYS and SYSTEM: "
read -s password1
green "Please retype the password for user SYS and SYSTEM: "
read -s password2
if test $password1 != $password2;then
   red "Sorry, passwords do not match."
else
   password=$password1
   break
fi
done

green "continue installing Oracle instance..."
sga_target=$(echo "`free -m|awk '/Mem/{print $2}'`*3/4" |bc)
cat >/home/oracle/dbca.rsp << EOF
[GENERAL]
RESPONSEFILE_VERSION = "11.2.0"
OPERATION_TYPE = "createDatabase"
[CREATEDATABASE]
GDBNAME = "$sid"
DATABASECONFTYPE  = "SI"
SID = "$sid"
TEMPLATENAME = "General_Purpose.dbc"
SYSPASSWORD = "$password"
SYSTEMPASSWORD = "$password"
DATAFILEDESTINATION=/data/oradata
RECOVERYAREADESTINATION=/data/oradata
STORAGETYPE=FS
CHARACTERSET="AL32UTF8"
INITPARAMS="memory_target=0,sga_target=${sga_target},pga_aggregate_target=1024,processes=800"
AUTOMATICMEMORYMANAGEMENT="False"
[CONFIGUREDATABASE]
[ADDINSTANCE]
DB_UNIQUE_NAME = "$sid"
NODENAME=
SYSDBAUSERNAME = "sys"
EOF
chown oracle:dba /home/oracle/dbca.rsp
su - oracle -c "~/product/11.2.4/dbhome_1/bin/dbca -silent -responseFile /home/oracle/dbca.rsp"
green "Cheers, guys, Oracle instance is installed successfully"

green "Starting to do post installation..."
mkdir -p /data/{arch_log,expdata}
chown -R oracle:dba /data/

# alter db path sql script
cat >/home/oracle/alterdbpath.sql <<EOF
alter database rename file "/data/oradata/${sid}/system01.dbf" to "/data/oradata/${sid}/datafile/system01.dbf";
alter database rename file "/data/oradata/${sid}/sysaux01.dbf" to "/data/oradata/${sid}/datafile/sysaux01.dbf";
alter database rename file "/data/oradata/${sid}/temp01.dbf" to "/data/oradata/${sid}/datafile/temp01.dbf";
alter database rename file "/data/oradata/${sid}/users01.dbf" to "/data/oradata/${sid}/datafile/users01.dbf";
alter database rename file "/data/oradata/${sid}/undotbs01.dbf" to "/data/oradata/${sid}/datafile/undotbs01.dbf";
alter database rename file "/data/oradata/${sid}/redo01.log" to "/data/oradata/${sid}/onlinelog/redo01.log";
alter database rename file "/data/oradata/${sid}/redo02.log" to "/data/oradata/${sid}/onlinelog/redo02.log";
alter database rename file "/data/oradata/${sid}/redo03.log" to "/data/oradata/${sid}/onlinelog/redo03.log";
EOF
sed -i "s/\"/'/g" /home/oracle/alterdbpath.sql
chown oracle:dba /home/oracle/alterdbpath.sql


su - oracle -c "sqlplus / as sysdba" <<EOF
alter system set db_recovery_file_dest='' scope=both;
shutdown immediate;
EOF

mkdir -p /data/oradata/${sid}/{onlinelog,controlfile,datafile}
chown -R oracle:dba /data/
mv /data/oradata/${sid}/control0* /data/oradata/${sid}/controlfile/
mv /data/oradata/${sid}/redo* /data/oradata/${sid}/onlinelog/
mv /data/oradata/${sid}/*.dbf /data/oradata/${sid}/datafile/

su - oracle -c "sqlplus / as sysdba" <<EOF
startup nomount;
alter system set control_files="/data/oradata/${sid}/controlfile/control01.ctl" scope=spfile;
shutdown immediate;
startup mount;
@/home/oracle/alterdbpath.sql;
alter database archivelog;
alter system set log_archive_dest_1='location=/data/arch_log' scope=both;
alter database open;
create directory expbak as '/data/expdata';
grant read,write on directory expbak to system;
EOF

green "Post installation is completed."
# create static listener 
green "starting to create static listener.."
cat >$ORACLE_HOME/network/admin/listener.ora <<EOF
SID_LIST_LISTENER =
  (SID_LIST =
    (SID_DESC =
      (GLOBAL_DBNAME = $sid)
      (ORACLE_HOME = ${ORACLE_HOME})
      (SID_NAME = $sid)
    )
  )

LISTENER =
  (DESCRIPTION_LIST =
    (DESCRIPTION =
      (ADDRESS = (PROTOCOL = TCP)(HOST = ${host_name})(PORT = 1521))
      (ADDRESS = (PROTOCOL = IPC)(KEY = EXTPROC1521))
    )
  )

ADR_BASE_LISTENER = /home/oracle
INBOUND_CONNECT_TIMEOUT_LISTENER=0
DIAG_ADR_ENABLED_LISTENER=OFF
EOF
chown oracle:dba $ORACLE_HOME/network/admin/listener.ora
su - oracle -c "lsnrctl start"
green "Listener is created successfully! "
