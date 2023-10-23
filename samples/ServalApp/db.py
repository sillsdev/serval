from sqlalchemy.orm import declarative_base
from sqlalchemy import Column, MetaData, String, Enum, create_engine
import enum


class State(enum.Enum):
    Pending = 0
    Active = 1
    Completed = 2
    Faulted = 3


metadata = MetaData()
Base = declarative_base(metadata=metadata)


class Build(Base):
    __tablename__ = "builds"
    build_id = Column("build_id", String, primary_key=True)
    engine_id = Column("engine_id", String, primary_key=True)
    email = Column("email", String)
    state = Column("state", Enum(State))
    corpus_id = Column("corpus_id", String)

    def __str__(self):
        return str(
            {
                "build_id": self.build_id,
                "engine_id": self.engine_id,
                "email": self.email,
                "state": self.state,
                "corpus_id": self.corpus_id,
            }
        )

    def __repr__(self):
        return self.__str__()


def clear_and_regenerate_tables():
    engine = create_engine("sqlite:///builds.db")
    metadata.drop_all(bind=engine)
    metadata.create_all(bind=engine)
