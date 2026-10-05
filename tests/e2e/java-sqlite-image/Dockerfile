# Reference image: the pre-rewrite Java exporter plus a SQLite JDBC driver,
# so the .NET rewrite can be diffed against the original against the same DB.
FROM prom-java-local

COPY sqlite-jdbc.jar slf4j-api.jar slf4j-nop.jar /app/
ENV JDBC_CP=app.jar:sqlite-jdbc.jar:slf4j-api.jar:slf4j-nop.jar
ENTRYPOINT ["sh", "-c", "java -cp \"$JDBC_CP\" com.ibm.jesseg.prometheus.MainApp"]
