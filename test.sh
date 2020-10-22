#!/bin/bash

##############################################################################
##                                                                          ##
##                   Oracle 11g silent install script                       ##
##                   --------------------------------                       ##
##                                             Author: 陳森、蔡洛洛         ##
##                                             Date:  2020/9/25             ##
##                                                                          ##
## The scope of this install script:                                        ##
##   OS version: CentOS6 / 7                                                ##
##   Oracle version: 11g / 11gR2                                            ##
##                                                                          ##
## The function of this install script:                                     ##
##   1. silent install database, single/multiple instance, listener         ##
##   2. adjust the path of datafile, redo logfile, archive logfile          ##
##      and control file                                                    ##
##   3. add the startup/shutdown script for Oralce                          ##
##                                                                          ##
##                                                                          ##
##############################################################################

function green(){ echo -e "\033[32m $1 \033[0m"; }
function red(){ echo -e "\033[31m\033[01m\033[05m $1 \033[0m"; }
unset http_proxy https_proxy
ipaddr=`ip a|awk '/global/{print substr($2,1,length($2)-3)}'`
host_name=`uname -n`


green "This process will install Oracle ADG. Please confirm if you're going to install ADG"
green "To confirm , please type y|yes or n|no"
while : 
do
  read option
  case $option in 
    y|yes)
         green "Now starting install Oracle ADG"
         break
         ;;
    n|no)
         red "Please execute the script ora_install_multi.sh for Oracle standalone installation"
         exit 0
         ;;
    *)
         red "input string is invaild, please input y|yes or n|no"
         ;;
   esac
done



# configure standby server ip address
while :
do
  green "We will deploy oracle ADG,the primary server ip is $ipaddr,please input standby server ip address"
  read standby_ip1
  green "Please input standby server ip address again"
  read standby_ip2
  green "Checking IP address..."

  if test ${standby_ip1} == ${standby_ip2};then
     echo ${standby_ip1} |awk -F"." '{if($1*1<255 && $2*1<255 && $3*1<255 && $4*1<255)print $0}' |grep -oP '^([1-9]\d{0,2})\.([1-9]\d{0,2})\.([1-9]\d{0,2})\.([1-9]\d{0,2})$'
            if [ $? -eq 0 ];then
           green "checking ip address pass"
           standby_ip=${standby_ip1}
           echo ${standby_ip}
           break
            else
                red "Please input the correct  ip address !"
            fi
        else
      red "The IP addresses that you input do not match!!"
   fi
done


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
function cfg_local_yumrepo(){
rm -rf /etc/yum.repos.d/*
if [ $OS_VER == "CentOS7" ];then
curl -o /etc/yum.repos.d/centos7.repo http://10.67.51.164/repofile/centos7.repo
fi

if [ $OS_VER == "CentOS6" ];then
curl -o /etc/yum.repos.d/centos6.repo http://10.67.51.164/repofile/centos6.repo
fi

sed -i '/^proxy/s/^proxy/#proxy/' /etc/yum.conf
}
cfg_local_yumrepo

# yum install prerequisite packages
function inst_pkgs(){
green "starting install these prerequisite packages for Oracle..."

rpm -ivh http://10.67.50.92/Tools/pdksh-5.2.14-37.el5.x86_64.rpm

yum install -y expect tree bc ntp wget xorg-x11-xauth unzip ftp gcc libaio libaio-devel compat-libstdc++-33 \
glibc-devel glibc-headers gcc-c++ sysstat \
elfutils-libelf-devel \
xorg-x11-server-utils \
rlwrap

green "checking if there are missing packages..."
rpm -q --qf '%{NAME}-%{VERSION}-%{RELEASE}(%{ARCH})\n' \
ntp expect tree bc wget xorg-x11-xauth unzip ftp gcc libaio libaio-devel compat-libstdc++-33 \
glibc-devel glibc-headers gcc-c++ sysstat \
elfutils-libelf-devel xorg-x11-server-utils rlwrap
[ $? -gt 0 ] && red "there are missing packages " && exit 1
}

inst_pkgs

# Common OS configuration

#--------------stop & disable firewall service and set timezone to Asia/Shanghai ---------------

function common_setup(){
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
   red "None of the above conditions is met"
fi

# configure sysctl.conf
mem_shmmax=`free -b|awk '/Mem/{print $2}'`
mem_shmall=`expr $mem_shmmax / 4096`

sed -i '/^kernel.shmall/s/kernel.shmall/#kernel.shmall/' /etc/sysctl.conf
sed -i '/^kernel.shmmax/s/kernel.shmmax/#kernel.shmmax/' /etc/sysctl.conf
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
}

common_setup

# add Oracle groups
function add_ora_grp(){
green "starting add groups for Oracle..."
for u in oinstall dba oper
do
grep -qw $u /etc/group
[ $? -gt 0 ] && groupadd $u
done
}

add_ora_grp

# add Oracle user
function add_ora_user(){
green "starting add user oracle..."
useradd oracle -g oinstall -G dba,oper
echo "oracle:Oracle@cesbg.foxconn.com99" | chpasswd
green "checking the group of user oracle..."
groups oracle
}

add_ora_user

#  Download Oracle installation packages
function get_oracle(){
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
      ora_ver=11.2.2
      wget -O /home/oracle/software/p10098816_112020_Linux-x86-64_1of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p10098816_112020_Linux-x86-64_1of7.zip
      wget -O /home/oracle/software/p10098816_112020_Linux-x86-64_2of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p10098816_112020_Linux-x86-64_2of7.zip
      break
      ;;
  2)
      ora_ver=11.2.3
      wget -O /home/oracle/software/p10404530_112030_Linux-x86-64_1of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p10404530_112030_Linux-x86-64_1of7.zip
      wget -O /home/oracle/software/p10404530_112030_Linux-x86-64_2of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p10404530_112030_Linux-x86-64_2of7.zip
      break
      ;;
  3)
      ora_ver=11.2.4
      wget -O /home/oracle/software/p13390677_112040_Linux-x86-64_1of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p13390677_112040_Linux-x86-64_1of7.zip
      wget -O /home/oracle/software/p13390677_112040_Linux-x86-64_2of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p13390677_112040_Linux-x86-64_2of7.zip
      break
      ;;
  *)
      green "Your option is not valid. Please choose (1)11.2.2  (2)11.2.3   (3)11.2.4"
      ;;
  esac
done

(cd /home/oracle/software;ls *.zip |xargs -n1 unzip )
chown -R oracle:oinstall /home/oracle/software/database
mkdir -p /data
chown -R oracle:dba /data
mkdir -p /home/oracle/product/${ora_ver}/dbhome_1
mkdir -p /home/oracle/oraInventory
chown -R oracle:dba /home/oracle
chmod -R 755 /home/oracle
}

get_oracle

# configure Oracle SID
function inputsid(){

while :
do
  green "Please input the Oracle SID: "
  read sid
  echo ${sid} |grep -qP '\W'
  if [ $? -eq 0 ]; then
    red "Don't input special character!! Please input SID again "
  else 
  break
  fi
done
}

inputsid

# configure .bash_profile under root account
function oraenv(){
export ORACLE_BASE=/home/oracle
export ORACLE_HOME=/home/oracle/product/${ora_ver}/dbhome_1
cat >>/home/oracle/.bash_profile << EOF
umask 022
unset USERNAME
export TMPDIR=/tmp
export ORACLE_BASE=/home/oracle
export ORACLE_SID=${sid}
export ORACLE_HOME=/home/oracle/product/${ora_ver}/dbhome_1
export ORACLE_TERM=xterm
export TNS_ADMIN=$ORACLE_HOME/network/admin
export LD_LIBRARY_PATH=$ORACLE_HOME/lib:/lib:/usr/lib:/usr/openwin/lib:/usr/local/lib
export PATH=$ORACLE_HOME/bin:$PATH
stty erase  ^H
alias sqlplus='rlwrap sqlplus'
alias rman='rlwrap rman'
EOF
}

oraenv

# customize db_install.rsp
function db_silent_install(){
source /home/oracle/.bash_profile
green "The next step will install Oracle software only, please key in 'y|yes'  or 'n|no'"

while :
do
  read db_sw_install
  case $db_sw_install in
  y|yes)
    green "continue install Oracle software..."
    break
      ;;
  n|no)
    green "the script will be quit now."
      exit 0

      ;;
  *)
      red "input string is invaild, please input y|yes or n|no"
      ;;
  esac
done

#  configure db_install.rsp file
sed -i 's/^oracle.install.option=/oracle.install.option=INSTALL_DB_SWONLY/' /home/oracle/software/database/response/db_install.rsp
host_name=`uname -n`; sed -i 's/^ORACLE_HOSTNAME=/ORACLE_HOSTNAME='${host_name}'/' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^UNIX_GROUP_NAME=/UNIX_GROUP_NAME=oinstall/' /home/oracle/software/database/response/db_install.rsp
sed -i 's#^INVENTORY_LOCATION=#INVENTORY_LOCATION=/home/oracle/oraInventory#' /home/oracle/software/database/response/db_install.rsp
sed -i 's#^ORACLE_HOME=#ORACLE_HOME='${ORACLE_HOME}'#' /home/oracle/software/database/response/db_install.rsp
sed -i 's#^ORACLE_BASE=#ORACLE_BASE='${ORACLE_BASE}'#' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^oracle.install.db.InstallEdition=/oracle.install.db.InstallEdition=EE/' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^oracle.install.db.DBA_GROUP=/oracle.install.db.DBA_GROUP=dba/' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^oracle.install.db.OPER_GROUP=/oracle.install.db.OPER_GROUP=oper/' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^DECLINE_SECURITY_UPDATES=/DECLINE_SECURITY_UPDATES=true/' /home/oracle/software/database/response/db_install.rsp

su - oracle -c "cd /home/oracle/software/database/;./runInstaller -silent -force -waitforcompletion  -responseFile /home/oracle/software/database/response/db_install.rsp -ignorePrereq"
sh /home/oracle/oraInventory/orainstRoot.sh
sh /home/oracle/product/${ora_ver}/dbhome_1/root.sh

green "Cheers guys, Oracle is installed successfully"
}

 db_silent_install

green "next we will install oracle for standby server ..."

#  Define stand_by DB script
function standby_sp()
{
#echo "please input oracle_ver"
#read ora_ver
#echo echo "oracle_sid=${ora_ver}"
#echo "please input oracle_sid"
#read sid
#echo "oracle_sid=${sid}"

cat >>/home/dbadmin/standby_sp.sh << FFF
unset http_proxy https_proxy
ipaddr=\`ip a|awk '/global/{print substr(\$2,1,length(\$2)-3)}'\`
host_name=\`uname -n\`

cat >>/etc/hosts <<EOF
\$ipaddr    \${host_name}
EOF

function green(){
echo -e "\033[32m \$1 \033[0m"
}

function red(){
echo -e "\033[31m\033[01m\033[05m \$1 \033[0m"
}

# check OS version
checkos(){
[ \`cat /etc/*release  |grep -i centos |grep -c "7\."\` -gt 0 ] && OS_VER="CentOS7"
[ \`cat /etc/*release  |grep -i centos |grep -c "6\."\` -gt 0 ] && OS_VER="CentOS6"
}


# configure yum local repository
function cfg_local_yumrepo(){
rm -rf /etc/yum.repos.d/*
if [ \$OS_VER == "CentOS7" ];then
curl -o /etc/yum.repos.d/centos7.repo http://10.67.51.164/repofile/centos7.repo
fi

if [ \$OS_VER == "CentOS6" ];then
curl -o /etc/yum.repos.d/centos6.repo http://10.67.51.164/repofile/centos6.repo
fi

sed -i '/^proxy/s/^proxy/#proxy/' /etc/yum.conf
}


# yum install prerequisite packages
function inst_pkgs(){
green "starting install these prerequisite packages for Oracle..."

rpm -ivh http://10.67.50.92/Tools/pdksh-5.2.14-37.el5.x86_64.rpm

yum install -y tree bc ntp wget xorg-x11-xauth unzip ftp gcc libaio libaio-devel compat-libstdc++-33 \\
   glibc-devel glibc-headers gcc-c++ sysstat \\
   elfutils-libelf-devel \\
   xorg-x11-server-utils \\
   rlwrap

green "checking if there are missing packages..."
 rpm -q --qf '%{NAME}-%{VERSION}-%{RELEASE}(%{ARCH})\n' \\
 ntp wget xorg-x11-xauth unzip ftp gcc libaio libaio-devel compat-libstdc++-33 \\
 glibc-devel glibc-headers gcc-c++ sysstat \\
 elfutils-libelf-devel xorg-x11-server-utils rlwrap
[ \$? -gt 0 ] && red "there are missing packages " && exit 1
}


# Common OS configuration

#--------------stop & disable firewall service and set timezone to Asia/Shanghai ---------------

function common_setup(){
if [ \$OS_VER == "CentOS7" ];then
timedatectl set-timezone 'Asia/Shanghai'
systemctl stop firewalld
systemctl disable firewalld
fi

if [ \$OS_VER == "CentOS6" ];then
/etc/init.d/iptables stop
chkconfig iptables off
ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime
fi

#---------------disable selinux-----------------
selinux_status=\`getenforce\`
if [ \$selinux_status != "Disabled" ];then
  setenforce 0
  sed -i '/^SELINUX=/s/enforcing/disabled/' /etc/selinux/config
fi

#--------------- setup ntp service----------------
rpm -q chrony
ret=\$?
if [ \$ret -gt 0 ] && [ "\$OS_VER" = "CentOS6" ];then
service ntpd stop
ntpdate 10.67.50.111
sed -i '/restrict ::1/a\server 10.67.50.111\nserver 10.191.131.131' /etc/ntp.conf
service ntpd start

elif [ \$ret -gt 0 ] && [ "\$OS_VER" = "CentOS7" ];then
systemctl stop ntpd
ntpdate 10.67.50.111
sed -i '/restrict ::1/a\server 10.67.50.111\nserver 10.191.131.131' /etc/ntp.conf
systemctl start ntpd

elif  [ \$ret -eq 0 ];then
systemctl stop chronyd.service
systemctl disable chronyd.service
systemctl stop ntpd
ntpdate 10.67.50.111
sed -i '/restrict ::1/a\server 10.67.50.111\nserver 10.191.131.131' /etc/ntp.conf
systemctl start ntpd

else
   red "None of the above conditions is met"
fi

# configure sysctl.conf
mem_shmmax=\`free -b|awk '/Mem/{print \$2}'\`
mem_shmall=\`expr \$mem_shmmax / 4096\`

sed -i '/^kernel.shmall/s/kernel.shmall/#kernel.shmall/' /etc/sysctl.conf
sed -i '/^kernel.shmmax/s/kernel.shmmax/#kernel.shmmax/' /etc/sysctl.conf
cat >> /etc/sysctl.conf <<EOF
fs.aio-max-nr = 1048576
fs.file-max = 6815744
kernel.shmall = \${mem_shmall}
kernel.shmmax = \${mem_shmmax}
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
}



# add Oracle groups
function add_ora_grp(){
green "starting add groups for Oracle..."
for u in oinstall dba oper
do
grep -qw \$u /etc/group
[ \$? -gt 0 ] && groupadd \$u
done
}


# add Oracle user
function add_ora_user(){
green "starting add user oracle..."
useradd oracle -g oinstall -G dba,oper
echo "oracle:Oracle@cesbg.foxconn.com99" | chpasswd
green "checking the group of user oracle..."
groups oracle
}


#  Download Oracle installation packages
function get_oracle(){
if [ ! -d "/home/oracle/software" ]; then
mkdir -p /home/oracle/software
mkdir -p /home/oracle/run
fi

green "The primary DB version is $ora_ver, next we will download and install oracle $ora_ver"
ora_ver=${ora_ver}
  case \$ora_ver in
  11.2.2)
  wget -O /home/oracle/software/p10098816_112020_Linux-x86-64_1of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p10098816_112020_Linux-x86-64_1of7.zip
  wget -O /home/oracle/software/p10098816_112020_Linux-x86-64_2of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p10098816_112020_Linux-x86-64_2of7.zip
  ;;
  11.2.3)
  wget -O /home/oracle/software/p10404530_112030_Linux-x86-64_1of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p10404530_112030_Linux-x86-64_1of7.zip
  wget -O /home/oracle/software/p10404530_112030_Linux-x86-64_2of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p10404530_112030_Linux-x86-64_2of7.zip
  ;;
  11.2.4)
  wget -O /home/oracle/software/p13390677_112040_Linux-x86-64_1of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p13390677_112040_Linux-x86-64_1of7.zip
  wget -O /home/oracle/software/p13390677_112040_Linux-x86-64_2of7.zip http://10.67.50.92/Oracle/Oracle11G%20R2/p13390677_112040_Linux-x86-64_2of7.zip
  ;;
  esac

(cd /home/oracle/software;ls *.zip |xargs -n1 unzip )
chown -R oracle:oinstall /home/oracle/software/database
mkdir -p /data
chown -R oracle:dba /data
mkdir -p /home/oracle/product/${ora_ver}/dbhome_1
mkdir -p /home/oracle/oraInventory
chown -R oracle:dba /home/oracle
chmod -R 755 /home/oracle
}

# configure .bash_profile under root account
function oraenv(){
export ORACLE_BASE=/home/oracle
export ORACLE_HOME=/home/oracle/product/${ora_ver}/dbhome_1
cat >>/home/oracle/.bash_profile << EOF
umask 022
unset USERNAME
export TMPDIR=/tmp
export ORACLE_BASE=/home/oracle
export ORACLE_SID=${sid}
export ORACLE_HOME=/home/oracle/product/${ora_ver}/dbhome_1
export ORACLE_TERM=xterm
export TNS_ADMIN=\$ORACLE_HOME/network/admin
export LD_LIBRARY_PATH=\$ORACLE_HOME/lib:/lib:/usr/lib:/usr/openwin/lib:/usr/local/lib
export PATH=\$ORACLE_HOME/bin:\$PATH
stty erase  ^H
alias sqlplus='rlwrap sqlplus'
alias rman='rlwrap rman'
EOF
}
# list functions
checkos
cfg_local_yumrepo
inst_pkgs
common_setup
add_ora_grp
add_ora_user
get_oracle
oraenv

#  configure db_install.rsp file
sed -i 's/^oracle.install.option=/oracle.install.option=INSTALL_DB_SWONLY/' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^ORACLE_HOSTNAME=/ORACLE_HOSTNAME='\${host_name}'/' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^UNIX_GROUP_NAME=/UNIX_GROUP_NAME=oinstall/' /home/oracle/software/database/response/db_install.rsp
sed -i 's#^INVENTORY_LOCATION=#INVENTORY_LOCATION=/home/oracle/oraInventory#' /home/oracle/software/database/response/db_install.rsp
sed -i 's#^ORACLE_HOME=#ORACLE_HOME='\${ORACLE_HOME}'#' /home/oracle/software/database/response/db_install.rsp
sed -i 's#^ORACLE_BASE=#ORACLE_BASE='\${ORACLE_BASE}'#' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^oracle.install.db.InstallEdition=/oracle.install.db.InstallEdition=EE/' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^oracle.install.db.DBA_GROUP=/oracle.install.db.DBA_GROUP=dba/' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^oracle.install.db.OPER_GROUP=/oracle.install.db.OPER_GROUP=oper/' /home/oracle/software/database/response/db_install.rsp
sed -i 's/^DECLINE_SECURITY_UPDATES=/DECLINE_SECURITY_UPDATES=true/' /home/oracle/software/database/response/db_install.rsp

su - oracle -c "cd /home/oracle/software/database/;./runInstaller -silent -force -waitforcompletion  -responseFile /home/oracle/software/database/response/db_install.rsp -ignorePrereq"
sh /home/oracle/oraInventory/orainstRoot.sh
sh /home/oracle/product/${ora_ver}/dbhome_1/root.sh

green "Cheers guys, Oracle is installed successfully"
FFF
}

standby_sp


# config expect 
cat >/home/dbadmin/expect_rsa <<EOF
#!/usr/bin/expect
set timeout 10
set host [lindex \$argv 0]
set username [lindex \$argv 1]
set password [lindex \$argv 2]
spawn ssh-copy-id \$username@\$host
 expect {
 "(yes/no)?"
  {
    send "yes\n"
    expect "*assword:" { send "\$password\n"}
  }
 "*assword:"
  {
    send "\$password\n"
  }
}
expect "100%"
expect eof
EOF

chmod +x /home/dbadmin/expect_rsa
chown dbadmin:dbadmin /home/dbadmin/expect_rsa
su - dbadmin -c "ssh-keygen -q -f '/home/dbadmin/.ssh/id_rsa' -N ''  <<< y"
su -m - dbadmin -c "./expect_rsa ${standby_ip} dbadmin Foxconn123#@\!"


cat >/home/dbadmin/expect_scp <<EOF
#!/usr/bin/expect  
set timeout 10  
set host [lindex \$argv 0]  
set username [lindex \$argv 1]  
set password [lindex \$argv 2]  
set src_file [lindex \$argv 3]  
set dest_file [lindex \$argv 4]  
spawn scp \$src_file \$username@\$host:\$dest_file  
 expect {  
 "(yes/no)?"  
  {  
    send "yes\n"  
    expect "*assword:" { send "\$password\n"}  
  }  
 "*assword:"  
  {  
    send "\$password\n"  
  }  
}  
expect "100%"  
expect eof
EOF

chmod +x /home/dbadmin/expect_scp
chmod +x /home/dbadmin/standby_sp.sh
chown dbadmin:dbadmin /home/dbadmin/expect_scp
chown dbadmin:dbadmin /home/dbadmin/standby_sp.sh
su -m - dbadmin -c "./expect_scp ${standby_ip} dbadmin Foxconn123#@! /home/dbadmin/standby_sp.sh /home/dbadmin/standby_sp.sh"
#su -m - dbadmin -c "ssh -t dbadmin@${standby_ip} 'nohup sh /home/dbadmin/standby_sp.sh &'"
su -m - dbadmin -c "nohup ssh ${standby_ip} 'sudo sh /home/dbadmin/standby_sp.sh' &>standby_install.log &"

green "Primary DB instance  installation and standby oracle installation  will work at the same time"
green "Next we will install primary instance"
function inputpwd(){
while :
do
green "Please type the password for user SYS and SYSTEM: "
read -s password1
green "Please retype the password for user SYS and SYSTEM: "
read -s password2

if test ${password1} != ${password2};then
   red "Sorry, passwords do not match."
else
   password=$password1
   break
fi
done
}

inputpwd

#sga_target=$(echo "`free -m|awk '/Mem/{print $2}'`*3/4" |bc)
function inputsga(){
while :
do
  total_mem=`free -m|awk '/Mem/{print $2}'`
  green "The total memory on the machine is ${total_mem}MB. Please input the value of sga_target, of which the unit is MB."
  green "For instance: 4096 8192 16384 "
  green "The default pga memory is 1024MB."
  read sga_target1
  green "Please type the value of sga_target again: "
  read sga_target2

  if test ${sga_target1} != ${sga_target2};then
     red "Sorry, the value of sga_target do not match."
  else
     sga_target=${sga_target1}
     green "Starting install Oracle Instance..."
     green "This process will take a few minutes..."
     break
  fi
done
}

inputsga

function cfg_dbca_rsp(){
cat >/home/oracle/${sid}-dbca.rsp << EOF
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
chown oracle:dba /home/oracle/${sid}-dbca.rsp
}
cfg_dbca_rsp

function dbca_silent(){
su - oracle -c "~/product/11.2.4/dbhome_1/bin/dbca -silent -responseFile /home/oracle/${sid}-dbca.rsp"
green "Cheers guys, Oracle instance is installed successfully"
}

dbca_silent

function post_install(){
green "Starting post installation..."
mkdir -p /data/{arch_log,expdata}
chown -R oracle:dba /data/

# alter db path sql script
cat >/home/oracle/${sid}-alterdbpath.sql <<EOF
alter system set db_unique_name="${sid}" scope=spfile;
alter system set fal_client="prd_${sid}";
alter system set fal_server="sty_${sid}";
alter system set log_archive_config='DG_CONFIG=(${sid},${sid}_sty)';
alter database archivelog;
alter system set log_archive_dest_1='location=/data/arch_log' scope=both;
alter system set log_archive_dest_2='SERVICE=sty_${sid}  LGWR ASYNC VALID_FOR=(ONLINE_LOGFILES,PRIMARY_ROLE) DB_UNIQUE_NAME=${sid}_sty';
alter system set log_archive_dest_state_1='ENABLE';
alter system set log_archive_dest_state_2='ENABLE';
alter system set standby_file_management='AUTO';
alter system set db_file_name_convert='/data/oradata/${sid}/','/data/oradata/${sid}/' scope=spfile;
alter system set log_file_name_convert='/data/oradata/${sid}/','/data/oradata/${sid}/' scope=spfile;
alter system set db_recovery_file_dest='' scope=both;
alter database force logging;
alter database rename file "/data/oradata/${sid}/system01.dbf" to "/data/oradata/${sid}/datafile/system01.dbf";
alter database rename file "/data/oradata/${sid}/sysaux01.dbf" to "/data/oradata/${sid}/datafile/sysaux01.dbf";
alter database rename file "/data/oradata/${sid}/temp01.dbf" to "/data/oradata/${sid}/datafile/temp01.dbf";
alter database rename file "/data/oradata/${sid}/users01.dbf" to "/data/oradata/${sid}/datafile/users01.dbf";
alter database rename file "/data/oradata/${sid}/undotbs01.dbf" to "/data/oradata/${sid}/datafile/undotbs01.dbf";
alter database rename file "/data/oradata/${sid}/redo01.log" to "/data/oradata/${sid}/onlinelog/redo01.log";
alter database rename file "/data/oradata/${sid}/redo02.log" to "/data/oradata/${sid}/onlinelog/redo02.log";
alter database rename file "/data/oradata/${sid}/redo03.log" to "/data/oradata/${sid}/onlinelog/redo03.log";

EOF
sed -i "s/\"/'/g" /home/oracle/${sid}-alterdbpath.sql
chown oracle:dba /home/oracle/${sid}-alterdbpath.sql

su -m - oracle -c "export ORACLE_SID=${sid};sqlplus / as sysdba" <<EOF
shutdown immediate;
EOF

mkdir -p /data/oradata/${sid}/{onlinelog,controlfile,datafile}
chown -R oracle:dba /data/
mv /data/oradata/${sid}/control0* /data/oradata/${sid}/controlfile/
mv /data/oradata/${sid}/redo* /data/oradata/${sid}/onlinelog/
mv /data/oradata/${sid}/*.dbf /data/oradata/${sid}/datafile/

su -m - oracle -c "export ORACLE_SID=${sid};sqlplus / as sysdba" <<EOF
startup nomount;
alter system set control_files="/data/oradata/${sid}/controlfile/control01.ctl" scope=spfile;
shutdown immediate;
startup mount;
@/home/oracle/${sid}-alterdbpath.sql;

alter database open;
create directory expbak as '/data/expdata';
grant read,write on directory expbak to system;
create pfile from spfile;
EOF

green "Post installation is completed."
}

post_install

# create static listener
function new_listener(){
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
su -m - oracle -c "lsnrctl start"
green "Listener is created successfully! "
}

new_listener

# create standby static listener
green "starting to create standby static listener.."
cat >$ORACLE_BASE/listener.ora <<EOF
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
      (ADDRESS = (PROTOCOL = TCP)(HOST = ${standby_ip})(PORT = 1521))
      (ADDRESS = (PROTOCOL = IPC)(KEY = EXTPROC1521))
    )
  )
ADR_BASE_LISTENER = /home/oracle
INBOUND_CONNECT_TIMEOUT_LISTENER=0
DIAG_ADR_ENABLED_LISTENER=OFF
EOF

chown oracle:dba $ORACLE_BASE/listener.ora


function new_tnsnames(){
green "add tnsnames.ora..."
cat >$ORACLE_HOME/network/admin/tnsnames.ora <<EOF
sty_${sid} =
  (DESCRIPTION =
    (ADDRESS_LIST =
      (ADDRESS = (PROTOCOL = TCP)(HOST = ${standby_ip})(PORT = 1521))
    )
    (CONNECT_DATA =
      (SERVER = DEDICATED)
      (SERVICE_NAME = ${sid})
    )
  )
prd_${sid} =
  (DESCRIPTION =
    (ADDRESS_LIST =
      (ADDRESS = (PROTOCOL = TCP)(HOST = ${ipaddr})(PORT = 1521))
    )
    (CONNECT_DATA =
      (SERVER = DEDICATED)
      (SERVICE_NAME = ${sid})
    )
  )
EOF
}

new_tnsnames

function add_db_script(){
mkdir -p /home/oracle/run
chown -R oracle:oinstall /home/oracle/run
cat >/home/oracle/run/DB_startup.sh<< EEE
export ORACLE_BASE=/home/oracle
export ORACLE_HOME=/home/oracle/product/${ora_ver}/dbhome_1
export PATH=$ORACLE_HOME/bin:$PATH
export ORACLE_SID=${sid}
sqlplus /nolog <<EOF
connect / as sysdba;
startup;
exit;
EOF
lsnrctl <<EOF
start
exit
EOF
EEE
cat >/home/oracle/run/DB_shutdown.sh<< EEE
export ORACLE_BASE=/home/oracle
export ORACLE_HOME=/home/oracle/product/${ora_ver}/dbhome_1
export PATH=$ORACLE_HOME/bin:$PATH
export ORACLE_SID=${sid}
sqlplus /nolog <<EOF
connect / as sysdba;
shutdown immediate;
exit;
EOF
EEE

cat >> /etc/rc.local <<EOF
su - oracle -c 'sh /home/oracle/run/DB_startup.sh' 1>/home/oracle/run/DB_startup.log 2>/home/oracle/run/DB_startup.err
EOF
chmod +x /etc/rc.local
chown -R oracle:oinstall /home/oracle/run
su -m - oracle -c "ln -s $ORACLE_BASE/diag/rdbms/${sid}/${sid}/trace/alert_${sid}.log alert_${sid}.log"
echo set sqlprompt '"'_user"'"@"'"_connect_identifier"'"\> "'"'"' >>/home/oracle/product/${ora_ver}/dbhome_1/sqlplus/admin/glogin.sql
}

add_db_script



chown oracle:dba $ORACLE_HOME/network/admin/tnsnames.ora
mv /home/dbadmin/expect_scp /home/oracle/
chown oracle:dba /home/oracle/expect_scp
source /home/oracle/.bash_profile
su -m - oracle -c "./expect_scp ${standby_ip} oracle Oracle@cesbg.foxconn.com99 $ORACLE_HOME/network/admin/tnsnames.ora $ORACLE_HOME/network/admin/tnsnames.ora"
su -m - oracle -c "./expect_scp ${standby_ip} oracle Oracle@cesbg.foxconn.com99 $ORACLE_HOME/dbs/orapw${sid} $ORACLE_HOME/dbs/"
su -m - oracle -c "cp $ORACLE_HOME/dbs/init${sid}.ora $ORACLE_HOME/dbs/init${sid}_tmp.ora"
su -m - oracle -c "./expect_scp ${standby_ip} oracle Oracle@cesbg.foxconn.com99 $ORACLE_BASE/listener.ora $ORACLE_HOME/network/admin/listener.ora"
su -m - oracle -c "./expect_scp ${standby_ip} oracle Oracle@cesbg.foxconn.com99 $ORACLE_BASE/run/DB_startup.sh $ORACLE_BASE/run/DB_startup.sh"
su -m - oracle -c "./expect_scp ${standby_ip} oracle Oracle@cesbg.foxconn.com99 $ORACLE_BASE/run/DB_shutdown.sh $ORACLE_BASE/run/DB_shutdown.sh"



sed  -i "s/*.db_unique_name='${sid}'/*.db_unique_name='${sid}_sty'/" $ORACLE_HOME/dbs/init${sid}_tmp.ora
sed  -i "s/*.fal_client='prd_${sid}'/*.fal_client='sty_${sid}'/" $ORACLE_HOME/dbs/init${sid}_tmp.ora
sed  -i "s/*.fal_server='sty_${sid}'/*.fal_server='prd_${sid}'/" $ORACLE_HOME/dbs/init${sid}_tmp.ora
sed  -i "/^*.log_archive_dest_2/s/DB_UNIQUE_NAME=${sid}_sty/DB_UNIQUE_NAME=${sid}/" $ORACLE_HOME/dbs/init${sid}_tmp.ora
sed  -i "/^*.log_archive_dest_2/s/SERVICE=sty_${sid}/SERVICE=prd_${sid}/" $ORACLE_HOME/dbs/init${sid}_tmp.ora
su -m - oracle -c "./expect_scp ${standby_ip} oracle Oracle@cesbg.foxconn.com99 $ORACLE_HOME/dbs/init${sid}_tmp.ora $ORACLE_HOME/dbs/init${sid}.ora"



cat >/home/dbadmin/standby_path.sh <<EEE

mkdir -p /data/{arch_log,expdata,oradata}
mkdir -p /data/oradata/${sid}/{controlfile,datafile,onlinelog}
mkdir -p /home/oracle/admin/${sid}/adump
chown -R oracle:dba /data/
chown -R oracle:dba /home/oracle/admin/${sid}/adump/

sed -i 's/startup;/startup mount;/'  $ORACLE_BASE/run/DB_startup.sh
sed -i '/startup mount;/a\alter database recover managed standby database disconnect from session;'  $ORACLE_BASE/run/DB_startup.sh
sed -i '/shutdown immediate;/i\alter database recover managed standby database cancel;' $ORACLE_BASE/run/DB_shutdown.sh

su - oracle -c "lsnrctl start"
su - oracle -c "lsnrctl reload"
su - oracle -c "sqlplus / as sysdba  <<EOF
create spfile from pfile;
startup  nomount;
EOF"

su - oracle -c "rman target sys/sys123sys@prd_${sid} auxiliary  sys/sys123sys@sty_${sid} <<EOF
duplicate target database for standby from active database nofilenamecheck dorecover;
EOF
"
su - oracle -c "sqlplus / as sysdba  <<EOF
alter database open;
alter database recover managed standby database disconnect from session;
alter database recover managed standby database cancel;
shutdown immediate;
startup mount;
alter database recover managed standby database disconnect from session;
EOF"

cat >> /etc/rc.local <<EOF
su - oracle -c 'sh /home/oracle/run/DB_startup.sh' 1>/home/oracle/run/DB_startup.log 2>/home/oracle/run/DB_startup.err
EOF
chmod +x /etc/rc.local
chown -R oracle:oinstall /home/oracle/run
echo set sqlprompt '"'_user"'"@"'"_connect_identifier"'"\> "'"'"' >>/home/oracle/product/${ora_ver}/dbhome_1/sqlplus/admin/glogin.sql
su -m - oracle -c "ln -s $ORACLE_BASE/diag/rdbms/${sid}_sty/${sid}/trace/alert_${sid}.log alert_${sid}.log"

EEE

mv /home/oracle/expect_scp /home/dbadmin/
chown dbadmin:dbadmin /home/dbadmin/expect_scp
chown dbadmin:dbadmin /home/dbadmin/standby_path.sh
chmod +x /home/dbadmin/standby_path.sh
su -m - dbadmin -c"./expect_scp ${standby_ip} dbadmin Foxconn123#@! /home/dbadmin/standby_path.sh /home/dbadmin/"
su -m - dbadmin -c "nohup ssh ${standby_ip} 'sudo sh /home/dbadmin/standby_path.sh' &>standby_path.log &"

exit;
EOF
lsnrctl <<EOF
start
exit
EOF
EEE
cat >/home/oracle/run/DB_shutdown.sh<< EEE
export ORACLE_BASE=/home/oracle
export ORACLE_HOME=/home/oracle/product/${ora_ver}/dbhome_1
export PATH=$ORACLE_HOME/bin:$PATH
export ORACLE_SID=${sid}
sqlplus /nolog <<EOF
connect / as sysdba;
shutdown immediate;
exit;
EOF
EEE

cat >> /etc/rc.local <<EOF
su - oracle -c 'sh /home/oracle/run/DB_startup.sh' 1>/home/oracle/run/DB_startup.log 2>/home/oracle/run/DB_startup.err
EOF
chmod +x /etc/rc.local
chown -R oracle:oinstall /home/oracle/run
echo set sqlprompt '"'_user"'"@"'"_connect_identifier"'"\> "'"'"' >>/home/oracle/product/${ora_ver}/dbhome_1/sqlplus/admin/glogin.sql
}

add_db_script

#function apd_db_script(){
#sed -i '/EOF/a export ORACLE_SID=${sid}\nsqlplus /nolog <<EOF \nconnect / as sysdba;\nstartup;\nexit;\nEOF' /home/oracle/run/DB_startup.sh
#sed -i '/EOF/a export ORACLE_SID=${sid}\nsqlplus /nolog <<EOF \nconnect / as sysdba;\nshutdown immediate;\nexit;\nEOF' /home/oracle/run/DB_shutdown.sh

#cat >> /home/oracle/run/DB_startup.sh<< EEE
#export ORACLE_SID=${sid}
#sqlplus /nolog <<EOF
#connect / as sysdba;
#startup;
#exit;
#EOF
#EEE

#cat >> /home/oracle/run/DB_shutdown.sh<< EEE
#export ORACLE_SID=${sid}
#sqlplus /nolog <<EOF
#conn / as sysdba
#shutdown immediate;
#exit;
#EOF
#EEE

#}
#apd_db_script


# create the 1st Oracle instance

#checkos

#cfg_local_yumrepo

#inst_pkgs

#common_setup

#add_ora_grp

#add_ora_user

#get_oracle

#inputsid

#oraenv

#db_silent_install

# run dbca silent install
#green "starting install Oracle instance now..."


#inputpwd

#inputsga

#cfg_dbca_rsp

#dbca_silent

#post_install

#new_listener

#add_db_script
# ask if create the 2nd Oracle instance

#while :
#do
#green "Would you like to install more Oracle instances ?"
#green "Please type y|yes or n|no"
#read answ
#  case $answ in
#   y|yes)
#    green "Now install the 2nd Oracle instance..."
#
#    # input the Oracle SID
#    inputsid
#
#    # input the password for user SYS and SYSTEM for Oracle instance.
#    inputpwd
#
#    # input the value of sga for Oracle instance.
#    inputsga
#
#    # configure the dbca.rsp file
#    cfg_dbca_rsp
#
#    # dbca silent install Oracle instance
#    dbca_silent
#
#    # Post installation after instance creation.
#    post_install
#
#    # append another SID into listner.ora and reload listener
#    apd_sid
#	
#	# append another SID into db script
#	apd_db_script
#      ;;
#  n|no)
#    green "the script will be quit now."
#      exit 0
#
#      ;;
#  *)
#      red "input string is invaild, please key in y|yes or n|no"
#      ;;
#  esac
#done

